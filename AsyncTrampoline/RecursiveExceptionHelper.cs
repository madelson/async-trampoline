using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System;

namespace Medallion;

/// <summary>
/// Helper class for performantly throwing exceptions in deeply-recursive methods.
/// </summary>
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
