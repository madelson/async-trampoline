using Medallion;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AsyncTrampoline.Tests;

public class BasicTest
{
    [Test]
    public void FibTest()
    {
        Assert.AreEqual(55, Recursive.Run(Fib(10)));

        async Recursive<long> Fib(long n) => n switch
        {
            0 => 0,
            1 => 1,
            _ => await Fib(n - 1) + await Fib(n - 2)
        };
    }

    // https://github.com/dotnet/roslyn/issues/41222 originally blocked the implementation of this technique
    [Test]
    public void ReproOldCSharpBug()
    {
        Assert.AreEqual("bar", Recursive.Run(Foo(10)));

        async Recursive<string> Foo(int n) =>
            n <= 0 ? "bar" : await Foo(n - 1);
    }

    [Test]
    public void TestGarbageCollection()
    {
        Dictionary<int, WeakReference> weakReferences = new();
        Bar(10).ComputeResult();

        async Recursive<int> Bar(int i)
        {
            object @object = new();
            weakReferences.Add(i, new(@object));

            if (i <= 0)
            {
                return 1;
            }

            var result = await Bar(i - 1);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.IsFalse(weakReferences[i - 1].IsAlive);
            Assert.IsTrue(weakReferences[i].IsAlive);
            return result + @object.GetHashCode();
        }
    }

    [Test]
    public void TestDeepRecursion()
    {
        Assert.AreEqual("hi", Recursive.Run(Count(100000)));

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

    /// <summary>
    /// Without our stack trace truncation, this takes forever to finish due to O(n^2) string concat
    /// </summary>
    [Test]
    public void TestDeepThrow()
    {
        var count = 0;

        var exception = Assert.Throws<InvalidOperationException>(() => Recursive.Run(RecursiveMethodThatThrows(100000)));
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