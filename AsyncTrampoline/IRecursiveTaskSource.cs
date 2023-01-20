using System;
using System.Collections.Generic;
using System.Text;

namespace Medallion
{
    internal interface IRecursiveTaskSource<TResult>
    {
        ushort Version { get; }
        void SetException(Exception exception);
        void SetResult(TResult result);
        void OnCompleted(Action continuation, ushort version);
        TResult ConsumeResult(ushort version);
    }
}
