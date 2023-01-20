using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Medallion
{
    internal abstract class StateMachineBoxOld<TBox, TStateMachine>
        where TBox : StateMachineBoxOld<TBox, TStateMachine>
        where TStateMachine : IAsyncStateMachine
    {
        // caching mechanism based on 
        // https://github.com/dotnet/corert/blob/master/src/System.Private.CoreLib/shared/System/Runtime/CompilerServices/AsyncValueTaskMethodBuilderT.cs

        private const int MaxCacheSize = 10;
        private static int _cacheLock;
        private static int _cacheSize;
        private static TBox? _cache;

        [AllowNull, MaybeNull]
        internal TStateMachine StateMachine = default;
        private TBox? _next;
        private Action? _cachedMoveNextAction;

        public Action MoveNextAction => this._cachedMoveNextAction ??= this.MoveNext;
        public ushort Version { get; private set; }

        private void MoveNext() => this.StateMachine!.MoveNext();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TBox? GetCachedBoxOrDefault()
        {
            if (Interlocked.CompareExchange(ref _cacheLock, 1, 0) == 0)
            {
                var box = _cache;
                if (box != null)
                {
                    _cache = box._next;
                    box._next = null;
                    --_cacheSize;
                    Invariant.Require(_cacheSize >= 0);
                }

                Volatile.Write(ref _cacheLock, 0);
                return box;
            }

            return null;
        }

        protected static void ReturnOrDropBox(TBox box)
        {
            Invariant.Require(box._next is null);

            // don't allow a box to ever re-use a version
            if (unchecked(++box.Version) == 0)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _cacheLock, 1, 0) == 0)
            {
                if (_cacheSize < MaxCacheSize)
                {
                    box.StateMachine = default;
                    box._next = _cache;
                    _cache = box;
                    ++_cacheSize;
                    Invariant.Require(0 < _cacheSize && _cacheSize <= MaxCacheSize);
                }

                Volatile.Write(ref _cacheLock, 0);
            }
        }

        protected void ValidateVersion(ushort version)
        {
            if (version != this.Version)
            {
                ThrowBadVersion();
            }
        }

        private static void ThrowBadVersion() => throw new InvalidOperationException($"An instance of {typeof(RecursiveTask)} may only be awaited once and should not be used after that");
    }
}
