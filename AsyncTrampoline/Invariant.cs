using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Medallion
{
    internal static class Invariant
    {
        [Conditional("DEBUG")]
        public static void Require(bool condition)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Invariant violated");
            }
        }
    }
}
