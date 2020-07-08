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
