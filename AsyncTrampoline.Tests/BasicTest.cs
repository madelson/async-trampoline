using Medallion;
using NUnit.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AsyncTrampoline.Tests;

public class BasicTest
{
    [Test]
    public void FibTest()
    {
        Assert.AreEqual(55, Fib(10).GetResult());

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
        Assert.AreEqual("bar", Foo(10).GetResult());

        async Recursive<string> Foo(int n) =>
            n <= 0 ? "bar" : await Foo(n - 1);
    }

    [Test]
    public void TestDeepRecursion()
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