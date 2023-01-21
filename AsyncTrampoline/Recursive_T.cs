using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

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
        if (this._frame.State is RecursiveStackFrameState.Incomplete)
        {
            Medallion.RecursiveStackFrame? prevBox = null;
            Medallion.RecursiveStackFrame currentBox = this._frame;
            while (true)
            {
                currentBox.MoveNext();

                if (currentBox.State != RecursiveStackFrameState.Incomplete)
                {
                    if (prevBox is null)
                    {
                        Debug.Assert(currentBox == this._frame);
                        break;
                    }

                    currentBox = prevBox;
                    prevBox = (RecursiveStackFrame)prevBox.Value!; // back pointer
                }
                else
                {
                    Debug.Assert(currentBox.Value is not null);
                    Debug.Assert(currentBox.Value != prevBox);
                    var nextBox = (RecursiveStackFrame)currentBox.Value;
                    currentBox.SetValue(prevBox); // set up back pointer
                    prevBox = currentBox;
                    currentBox = nextBox!;
                }
            }
        }

        Debug.Assert(this._frame.State != RecursiveStackFrameState.Incomplete);
        return this._frame.GetResult(isOuterResult: true);
    }

    public Awaiter GetAwaiter() => new(this._frame);

    public readonly struct Awaiter : INotifyCompletion, IRecursiveAwaiter
    {
        private readonly RecursiveStackFrame _frame;

        internal Awaiter(RecursiveStackFrame box) { this._frame = box; }

        public bool IsCompleted => false;

        [StackTraceHidden]
        public TResult GetResult() => this._frame.GetResult(isOuterResult: false);

        // todo explicit impl & good error message
        public void OnCompleted(Action action) =>
            throw new NotSupportedException();

        public void OnCompleted<TNextResult>(ref RecursiveMethodBuilder<TNextResult> methodBuilder) =>
            methodBuilder.Task._frame.SetValue(this._frame);
    }

    // todo either don't nest these or nest the builder
    internal abstract class RecursiveStackFrame : Medallion.RecursiveStackFrame
    {
        [MaybeNull]
        private TResult _result;
        
        public void SetResult(TResult result)
        {
            Debug.Assert(this.State is RecursiveStackFrameState.Incomplete);
            this._result = result;
            this.State = RecursiveStackFrameState.RanToCompletion;
        }

        [StackTraceHidden]
        public TResult GetResult(bool isOuterResult)
        {
            if (this.State != RecursiveStackFrameState.RanToCompletion)
            {
                ThrowGetResultFailed(isOuterResult);
            }

            return this._result!;
        }

        [StackTraceHidden]
        private void ThrowGetResultFailed(bool isOuterResult)
        {
            switch (this.State)
            {
                case RecursiveStackFrameState.Incomplete:
                    // todo revisit with finalized API
                    throw new InvalidOperationException($"The result has not been computed yet. Did you mean to call {nameof(Recursive<TResult>)}<{nameof(TResult)}>.GetResult()?");
                case RecursiveStackFrameState.Faulted:
                    RecursiveExceptionHelper.Throw((Exception)this.Value!, isFinalThrow: isOuterResult);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected state {this.State}");
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
            Debug.Assert(this.State is RecursiveStackFrameState.Incomplete);
            this.StateMachine!.MoveNext();
        }
    }
}

internal abstract class RecursiveStackFrame
{
    /// <summary>
    /// This can be 3 things:
    /// * An <see cref="Exception"/> indicating a faulted recursion
    /// * A <see cref="RecursiveStackFrame"/> that we are awaiting
    /// * (During evaluation) a <see cref="RecursiveStackFrame"/> that we should return to after this frame completes
    /// </summary>
    public object? Value { get; private set; }

    public void SetValue(RecursiveStackFrame? frame)
    {
        Debug.Assert(this.State is RecursiveStackFrameState.Incomplete);
        this.Value = frame;
    }

    public void SetException(Exception exception)
    {
        Debug.Assert(this.State is RecursiveStackFrameState.Incomplete);
        this.Value = exception;
        this.State = RecursiveStackFrameState.Faulted;
    }

    public RecursiveStackFrameState State { get; protected set; }

    public abstract void MoveNext();
}

internal enum RecursiveStackFrameState : byte
{
    Incomplete = 0,
    RanToCompletion = 1,
    Faulted = 2,
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
    public void SetResult(TResult result) => this._frame.SetResult(result);
    public void SetException(Exception exception) => this._frame.SetException(exception);

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

internal static class RecursiveExceptionHelper
{
    private const int MaxThrowCount = 10;

    private static readonly ConditionalWeakTable<Exception, ExceptionState> ExceptionStates = new();

    [StackTraceHidden]
    public static void Throw(Exception exception, bool isFinalThrow = false)
    {
        var state = ExceptionStates.GetValue(exception, static _ => new());

        if (state.TruncatedDispatchInfo != null)
        {
            if (isFinalThrow)
            {
                ThrowRecursiveExceptionWithTruncatedStackTrace(state.TruncatedDispatchInfo);
            }
            state.TruncatedDispatchInfo.Throw();
        }

        var dispatchInfo = ExceptionDispatchInfo.Capture(exception);
        if (++state.ThrowCount >= MaxThrowCount)
        {
            state.TruncatedDispatchInfo = dispatchInfo;
        }
        dispatchInfo.Throw();
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // we want this visible in the trace
    private static void ThrowRecursiveExceptionWithTruncatedStackTrace(ExceptionDispatchInfo dispatchInfo) =>
        dispatchInfo.Throw();

    private sealed class ExceptionState
    {
        public int ThrowCount;
        public ExceptionDispatchInfo? TruncatedDispatchInfo;
    }
}