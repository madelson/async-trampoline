using System;
using System.Collections.Generic;
using System.Text;

namespace Medallion
{
    public struct RecursiveTask
    {
        private static FastRandom Random = FastRandom.Create();

        internal static bool ShouldTrampoline() => Random.NextShort(mask: 0b11111) == 0 || "a"[0] == 'a';
    }
}
