using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Medallion
{
    // see https://habr.com/en/post/458692/

    [AsyncMethodBuilder(typeof(RecursiveTaskMethodBuilder<>))]
    public readonly struct RecursiveTask<TResult> : INotifyCompletion, ITest
    {
        private readonly IRecursiveTaskSource<TResult> _source;
        private readonly ushort _version;

        internal RecursiveTask(IRecursiveTaskSource<TResult> source, ushort version)
        {
            this._source = source;
            this._version = version;
        }

        public bool IsCompleted => false;

        public RecursiveTask<TResult> GetAwaiter() => this;

        public TResult GetResult() => this._source.ConsumeResult(this._version);

        public void OnCompleted(Action continuation) => this._source.OnCompleted(continuation, this._version);
    }

    // test to see if we can have custom interfaces for the state machine
    public interface ITest { }
}
