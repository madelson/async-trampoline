using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Medallion
{
    // new proposal:
    // in start, we decide to either run MoveNext() right away or 
    // 

    public struct RecursiveTaskMethodBuilder<TResult>
    {
        private IRecursiveTaskSource<TResult>? _stateMachineBox;

        public static RecursiveTaskMethodBuilder<TResult> Create() => new RecursiveTaskMethodBuilder<TResult>();

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            unsafe
            {
                Console.WriteLine((IntPtr)Unsafe.AsPointer(ref this));
            }

            var stateMachineBox = StateMachineBox<TStateMachine>.GetCachedBoxOrDefault() ?? new StateMachineBox<TStateMachine>();
            this._stateMachineBox = stateMachineBox;
            var trampoline = Trampoline.Current;
            if (trampoline.ShouldTrampoline)
            {
                stateMachineBox.StateMachine = stateMachine;
                trampoline.PushAction(stateMachineBox.MoveNextAction);
            }
            else
            {
                stateMachine.MoveNext();
            }
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void SetException(Exception exception) => this.GetOrCreateRecursiveTaskSource().SetException(exception);

        public void SetResult(TResult result) => this.GetOrCreateRecursiveTaskSource().SetResult(result);

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter,
            ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion, ITest
            where TStateMachine : IAsyncStateMachine =>
            awaiter.OnCompleted(this.GetOrCreateStateMachineBox(ref stateMachine).MoveNextAction);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter,
            ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion, ITest
            where TStateMachine : IAsyncStateMachine =>
            awaiter.UnsafeOnCompleted(this.GetOrCreateStateMachineBox(ref stateMachine).MoveNextAction);

        private StateMachineBox<TStateMachine> GetOrCreateStateMachineBox<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            var existing = this._stateMachineBox;
            if (existing is null)
            {
                var result = StateMachineBox<TStateMachine>.GetCachedBoxOrDefault() ?? new StateMachineBox<TStateMachine>();
                this._stateMachineBox = result;
                result.StateMachine = stateMachine;
                return result;
            }

            return (StateMachineBox<TStateMachine>)existing;
        }

        // based on AsyncValueTaskMethodBuilderT.cs. Without this, I seem to get mysterious
        // crashes when debugging
        internal object ObjectIdForDebugger
        {
            get
            {
                if (this._stateMachineBox == null)
                {
                    this._stateMachineBox = this.GetOrCreateRecursiveTaskSource();
                }
                return this._stateMachineBox;
            }
        }

        public RecursiveTask<TResult> Task
        {
            get
            {
                var source = this._stateMachineBox;
                if (source is null)
                {
                    unsafe
                    {
                        Console.WriteLine((IntPtr)Unsafe.AsPointer(ref this));
                    }
                    
                    ThrowTaskAccessedTooEarly();
                }
                return new RecursiveTask<TResult>(source!, source!.Version);

                static void ThrowTaskAccessedTooEarly() =>
                    throw new InvalidOperationException("Cannot access the Task property before SetResult, SetException, or Await[Unsafe]OnCompleted has been called");
            }
        }

        // todo shouldn't need this except for debugger
        private IRecursiveTaskSource<TResult> GetOrCreateRecursiveTaskSource() => 
            this._stateMachineBox ??= (StateMachineBox<IAsyncStateMachine>.GetCachedBoxOrDefault() ?? new StateMachineBox<IAsyncStateMachine>());

        internal sealed class StateMachineBox<TStateMachine> : StateMachineBoxOld<StateMachineBox<TStateMachine>, TStateMachine>, IRecursiveTaskSource<TResult>
            where TStateMachine : IAsyncStateMachine
        {
            [AllowNull, MaybeNull] private TResult _result;
            private object? _obj;
            private Action? _continuation;

            public void OnCompleted(Action continuation, ushort version)
            {
                this.ValidateVersion(version);

                // note: under normal usage, OnCompleted is only called once so += is just =
                this._continuation += continuation;
                if (this._obj != null)
                {
                    this.PerformCompletion();
                }
            }

            public void SetException(Exception exception)
            {
                this._obj = exception;
                this.PerformCompletion();
            }

            public void SetResult(TResult result)
            {
                this._result = result;
                this._obj = Sentinels.Completed;
                this.PerformCompletion();
            }

            public TResult ConsumeResult(ushort version)
            {
                this.ValidateVersion(version);

                var obj = this._obj;
                if (obj is null)
                {
                    throw new NotImplementedException();
                }

                this._obj = null;

                if (obj != Sentinels.Completed)
                {
                    ReturnOrDropBox(this);
                    ExceptionDispatchInfo.Throw((Exception)obj);
                }

                var result = this._result;
                this._result = default;
                ReturnOrDropBox(this);
                return result!; // todo revisit !
            }

            private void PerformCompletion()
            {
                var continuation = this._continuation;
                this._continuation = null;
                continuation?.Invoke();
            }
        }
    }
}
