using Medallion;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AsyncTrampoline.Tests;

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

    [Test]
    public void Repro()
    {
        Foo(10).GetAwaiter().GetResult(); // throws "no state set"

        async MyTask<string> Foo(int n) =>
            n <= 0 ? "bar" : await Foo(n - 1);
    }

    [Test]
    public void Test()
    {
        var vt = Do(10);
        Console.WriteLine(vt.IsCompleted);
	        var b = GC.GetAllocatedBytesForCurrentThread();
        vt = Do(100);
        Console.WriteLine(GC.GetAllocatedBytesForCurrentThread() - b);
    }

    private static async Task Do(int i)
    {
        if (i == 0) { return; }

        await Do(i - 1);
    }

    [Test]
    public void Sandbox()
    {
        Assert.AreEqual(55, Fib(10).GetResult());

        async Recursive<long> Fib(long n) => n switch
        {
            0 => 0,
            1 => 1,
            _ => await Fib(n - 1) + await Fib(n - 2)
        };
    }

    [Test]
    public void TestDeepRecursion2()
    {
        Assert.AreEqual("hi", Count(100000).GetResult());

        async Recursive<string> Count(int n)
        {
            if (n <= 0)
            {
                return "hi";
            }

            RuntimeHelpers.EnsureSufficientExecutionStack();

            return await Count(n - 1);
        }
    }

    [Test]
    public void TestDeepThrow()
    {
        var count = 0;

        var exception = Assert.Throws<InvalidOperationException>(() => RecursiveMethodThatThrows(100000).GetResult());
        Assert.AreEqual(0, count);
        Assert.AreEqual("hi", exception.Message);
        Assert.AreEqual(1, Regex.Matches(exception.StackTrace, "ThrowRecursiveExceptionWithTruncatedStackTrace").Count, exception.StackTrace);
        Assert.AreEqual(10, Regex.Matches(exception.StackTrace, nameof(RecursiveMethodThatThrows)).Count, exception.StackTrace);

        async Recursive<long> RecursiveMethodThatThrows(int n)
        {
            ++count;
            try
            {
                if (n <= 0)
                {
                    throw new InvalidOperationException("hi");
                }

                RuntimeHelpers.EnsureSufficientExecutionStack();

                return await RecursiveMethodThatThrows(n - 1);
            }
            finally { --count; }
        }
    }
}