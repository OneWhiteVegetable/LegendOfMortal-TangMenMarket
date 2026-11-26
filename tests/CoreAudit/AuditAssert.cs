using System;
using System.Collections.Generic;

internal static class AuditAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                message + ": expected " + expected + ", actual " + actual);
        }
    }

    public static void Near(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.000001)
        {
            throw new InvalidOperationException(
                message + ": expected " + expected + ", actual " + actual);
        }
    }
}
