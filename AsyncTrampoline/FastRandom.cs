using System;
using System.Collections.Generic;
using System.Text;

namespace Medallion
{
    internal struct FastRandom
    {
        // implementation based on https://stackoverflow.com/questions/26237419/faster-than-rand

        private uint _state;

        public static FastRandom Create() => new FastRandom { _state = /*unchecked((uint)Guid.NewGuid().GetHashCode())*/ (uint)new Random(3024).Next() };

        public short NextShort(short mask = short.MaxValue)
        {
            unchecked
            {
                var newState = unchecked((214013 * this._state) + 2531011);
                this._state = newState;
                return (short)((newState >> 16) & mask);
            }
        }
    }
}
