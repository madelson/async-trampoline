using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Medallion
{
    // documented here: https://github.com/dotnet/roslyn/issues/41222
    [AsyncMethodBuilder(typeof(MyTaskMethodBuilder<>))]
    public readonly struct MyTask<TResult> : INotifyCompletion
    {
        internal MyTask(object state) { }

        public bool IsCompleted => false;

        public MyTask<TResult> GetAwaiter() => this;

        public TResult GetResult() => throw new NotImplementedException();

        public void OnCompleted(Action continuation) => throw new NotImplementedException();
    }

    public struct MyTaskMethodBuilder<TResult>
    {
        private object _state;

        public static MyTaskMethodBuilder<TResult> Create() => default;

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            this._state = new State<TStateMachine>();
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void SetException(Exception exception) => throw new NotImplementedException();

        public void SetResult(TResult result) => throw new NotImplementedException();

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter,
            ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine => throw new NotImplementedException();

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter,
            ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine => throw new NotImplementedException();

        public MyTask<TResult> Task => new MyTask<TResult>(this._state ?? throw new InvalidOperationException("no state set"));

        internal object ObjectIdForDebugger => this._state ??= new State<IAsyncStateMachine>();

        private class State<TStateMachine>
            where TStateMachine : IAsyncStateMachine
        {
        }
    }
}
