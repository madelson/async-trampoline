namespace Medallion;

public ref struct Recursive
{
    public static TResult Run<TResult>(Recursive<TResult> recursive) => recursive.GetResult();
}
