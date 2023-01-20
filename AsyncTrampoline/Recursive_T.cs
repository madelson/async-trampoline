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
    private readonly RecursiveStackFrame _frame;
    
    internal Recursive(RecursiveStackFrame frame)
    {
        this._frame = frame;
    }

    public TResult GetResult()
    {
        if (this._frame.HasResult) { return this._frame.Result!; }
        
        Medallion.RecursiveStackFrame? prevBox = null;
        Medallion.RecursiveStackFrame currentBox = this._frame;
        while (true)
        {
            currentBox.MoveNext();

            if (currentBox.HasResult)
            {
                if (prevBox is null)
                {
                    Debug.Assert(currentBox == this._frame);
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

        Debug.Assert(this._frame.HasResult);
        return this._frame.Result!;
    }

    public Awaiter GetAwaiter() => new(this._frame);

    public readonly struct Awaiter : INotifyCompletion, IRecursiveAwaiter
    {
        private readonly RecursiveStackFrame _frame;

        internal Awaiter(RecursiveStackFrame box) { this._frame = box; }

        public bool IsCompleted => false;

        public TResult GetResult()
        {
            Debug.Assert(this._frame.HasResult);
            return this._frame.Result!;
        }

        // todo explicit impl & good error message
        public void OnCompleted(Action action) =>
            throw new NotSupportedException();

        public void OnCompleted<TNextResult>(ref RecursiveMethodBuilder<TNextResult> methodBuilder) =>
            methodBuilder.Task._frame.Next = this._frame;
    }

    // todo either don't nest these or nest the builder
    internal abstract class RecursiveStackFrame : Medallion.RecursiveStackFrame
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

    internal sealed class RecursiveStackFrame<TStateMachine> : RecursiveStackFrame
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

internal abstract class RecursiveStackFrame
{
    public RecursiveStackFrame? Next { get; set; }

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
    private Recursive<TResult>.RecursiveStackFrame _frame;

    public static RecursiveMethodBuilder<TResult> Create() => new();

    public void SetStateMachine(IAsyncStateMachine stateMachine) => throw new NotSupportedException();
    public void SetResult(TResult result) => this._frame.Result = result;
    public void SetException(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();

    public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
    {
        Debug.Assert(this._frame is null);
        var frame = new Recursive<TResult>.RecursiveStackFrame<TStateMachine>();
        // NOTE: since this is a member of the state machine and the state machine will be a value type
        // in release builds, this line implicitly updates the state machine. Therefore, it must come before
        // we copy the state machine into the frame!
        this._frame = frame;
        frame.StateMachine = stateMachine;
    }

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine)
        where TAwaiter : IRecursiveAwaiter
        where TStateMachine : IAsyncStateMachine
    {
        Debug.Assert(
            this._frame is Recursive<TResult>.RecursiveStackFrame<TStateMachine> typedBox
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

    public Recursive<TResult> Task => new(this._frame);
}