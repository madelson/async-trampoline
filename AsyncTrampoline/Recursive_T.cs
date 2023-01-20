using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion;

[AsyncMethodBuilder(typeof(RecursiveMethodBuilder<>))]
public readonly ref struct Recursive<TResult>
{
    private readonly StateMachineBox _box;
    
    internal Recursive(StateMachineBox box)
    {
        this._box = box;
    }

    public TResult GetResult()
    {
        if (this._box.HasResult) { return this._box.Result!; }
        
        Medallion.StateMachineBox? prevBox = null;
        Medallion.StateMachineBox currentBox = this._box;
        while (true)
        {
            currentBox.MoveNext();

            if (currentBox.HasResult)
            {
                if (prevBox is null)
                {
                    Debug.Assert(currentBox == this._box);
                    break;
                }

                currentBox = prevBox;
                prevBox = prevBox.Next; // back pointer
            }   
            else
            {
                Debug.Assert(currentBox.Next is not null);
                Debug.Assert(currentBox.Next != prevBox);
                var nextBox = currentBox.Next;
                currentBox.Next = prevBox; // set up back pointer
                prevBox = currentBox;
                currentBox = nextBox!;
            }
        }

        Debug.Assert(this._box.HasResult);
        return this._box.Result!;
    }

    public Awaiter GetAwaiter() => new(this._box);

    public readonly struct Awaiter : INotifyCompletion, IRecursiveAwaiter
    {
        private readonly StateMachineBox _box;

        internal Awaiter(StateMachineBox box) { this._box = box; }

        public bool IsCompleted => false;

        public TResult GetResult()
        {
            Debug.Assert(this._box.HasResult);
            return this._box.Result!;
        }

        // todo explicit impl & good error message
        public void OnCompleted(Action action) =>
            throw new NotSupportedException();

        public void OnCompleted<TNextResult>(ref RecursiveMethodBuilder<TNextResult> methodBuilder) =>
            methodBuilder.Task._box.Next = this._box;
    }

    internal abstract class StateMachineBox : Medallion.StateMachineBox
    {
        [MaybeNull]
        private TResult _result;
        
        [MaybeNull]
        public TResult Result
        {
            get => this._result;
            set
            {
                Debug.Assert(!this.HasResult);
                this._result = value;
                this.HasResult = true;
            }
        }
    }

    internal sealed class StateMachineBox<TStateMachine> : StateMachineBox
        where TStateMachine : IAsyncStateMachine
    {
        [MaybeNull]
        public TStateMachine StateMachine;

        public override void MoveNext()
        {
            Debug.Assert(!this.HasResult);
            this.StateMachine!.MoveNext();
        }
    }
}

internal abstract class StateMachineBox
{
    public StateMachineBox? Next { get; set; }

    public bool HasResult { get; protected set; }

    public abstract void MoveNext();
}

public interface IRecursiveAwaiter
{
    void OnCompleted<TResult>(ref RecursiveMethodBuilder<TResult> methodBuilder);
}

public struct RecursiveMethodBuilder<TResult>
{
    [MaybeNull]
    private Recursive<TResult>.StateMachineBox _box;

    public static RecursiveMethodBuilder<TResult> Create() => new();

    public void SetStateMachine(IAsyncStateMachine stateMachine) => throw new NotSupportedException();
    public void SetResult(TResult result) => this._box.Result = result;
    public void SetException(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();

    public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
    {
        Debug.Assert(this._box is null);
        var box = new Recursive<TResult>.StateMachineBox<TStateMachine>();
        this._box = box; // must come before we put the state machine in the box!
        box.StateMachine = stateMachine;
    }

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine)
        where TAwaiter : IRecursiveAwaiter
        where TStateMachine : IAsyncStateMachine
    {
        Debug.Assert(
            this._box is Recursive<TResult>.StateMachineBox<TStateMachine> typedBox
                && (
                    typeof(TStateMachine).IsValueType
                        ? Unsafe.AreSame(ref stateMachine, ref typedBox.StateMachine)
                        : ReferenceEquals(stateMachine, typedBox.StateMachine)
                )
        );

        awaiter.OnCompleted(ref this);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine)
        where TAwaiter : IRecursiveAwaiter
        where TStateMachine : IAsyncStateMachine =>
        this.AwaitOnCompleted(ref awaiter, ref stateMachine);

    public Recursive<TResult> Task => new(this._box);
}