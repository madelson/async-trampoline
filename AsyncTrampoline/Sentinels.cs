using System;
using System.Collections.Generic;
using System.Text;

namespace Medallion
{
    // move to RecursiveTask.cs?
    internal static class Sentinels
    {
        public static object Completed = new object();
    }
}
