using System;
using System.Threading.Tasks;
using ReverseEngineering.WinForms.Debug;

class Program
{
    static async Task Main(string[] args)
    {
        string aion2Path = @"C:\Program Files (x86)\NCSOFT\AION2_TW\Aion2\Binaries\Win64\Aion2.exe";

        Console.WriteLine("=== Aion2.exe Advanced Debugger Test ===\n");
        Console.WriteLine($"Target: {aion2Path}\n");

        if (!System.IO.File.Exists(aion2Path))
        {
            Console.WriteLine("ERROR: Aion2.exe not found!");
            return;
        }

        Console.WriteLine("Test: Advanced Windows Debugger (Debug API)");
        Console.WriteLine("========================================");
        var advanced = new AdvancedWindowsDebugger();
        var result = await advanced.DebugBinaryAsync(aion2Path, (msg) => Console.Write(msg));
        Console.WriteLine($"\n========================================");
        Console.WriteLine($"Result: {result}\n");

        Console.WriteLine("=== Test Complete ===");
    }
}
