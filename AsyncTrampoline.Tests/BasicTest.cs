using Medallion;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace AsyncTrampoline.Tests
{
    public class BasicTest
    {
        [Test]
        public void FibTest()
        {
            Assert.AreEqual(55, Fib(10).GetAwaiter().GetResult());

            async RecursiveTask<long> Fib(long n) => n switch
            {
                0 => 0,
                1 => 1,
                _ => await Fib(n - 1) + await Fib(n - 2)
            };
        }

        [Test]
        public void TestDeepRecursion()
        {
            // idea:can't capture state machine in start(); that's too early

            Assert.AreEqual("hi", Count(100000).GetResult());

            async RecursiveTask<string> Count(int n)
            {
                if (n <= 0)
                {
                    return "hi";
                }

                RuntimeHelpers.EnsureSufficientExecutionStack();

                return await Count(n - 1);
            }
        }
    }
}
