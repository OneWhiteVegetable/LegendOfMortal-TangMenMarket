using System;
using System.Collections.Generic;

internal sealed class TestRunner
{
    private readonly List<string> _failures = new List<string>();

    public void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS " + name);
        }
        catch (Exception exception)
        {
            _failures.Add(name + ": " + exception.Message);
            Console.WriteLine("FAIL " + name + ": " + exception.Message);
        }
    }

    public int Complete()
    {
        if (_failures.Count == 0)
        {
            Console.WriteLine("ALL CORE AUDIT CHECKS PASSED");
            return 0;
        }

        Console.WriteLine("FAILURES: " + _failures.Count);
        for (int i = 0; i < _failures.Count; i++)
            Console.WriteLine(_failures[i]);
        return 1;
    }
}
