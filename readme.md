# C# MemberAccessGenerator

see: [Source Generator](https://github.com/dotnet/roslyn/blob/master/docs/features/source-generators.md).

Original source (manually written):

```cs
using MemberAccess;
using System;

[ByIndex]
partial record Point1(int X, int Y);

[ByName]
partial record Point2(int X, int Y);

namespace MemberAccessSample
{
    [ByIndex, ByName]
    partial record Point3(int X, int Y);

    partial class Program
    {
        static void Main()
        {
            var p1 = new Point1(1, 2);
            Console.WriteLine((p1.GetMember(0), p1.GetMember(1)));

            var p2 = new Point2(1, 2);
            Console.WriteLine((p2.GetMember("X"), p2.GetMember("Y")));

            var p3 = new Point3(1, 2);
            Console.WriteLine((p3.GetMember(0), p3.GetMember(1)));
            Console.WriteLine((p3.GetMember("X"), p3.GetMember("Y")));
        }
    }
}
```

Generated source:

```cs
partial record Point1
{
    public object GetMember(int index) => index switch
    {
        0 => X,
        1 => Y,
        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(),
    };
}

partial record Point2
{
    public object GetMember(string name) => name switch
    {
        nameof(X) => X,
        nameof(Y) => Y,
        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(),
    };
}

namespace Sample {
partial record Point3
{
    public object GetMember(int index) => index switch
    {
        0 => X,
        1 => Y,
        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(),
    };

    public object GetMember(string name) => name switch
    {
        nameof(X) => X,
        nameof(Y) => Y,
        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(),
    };
}
}
```
