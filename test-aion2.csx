#!/usr/bin/env dotnet-script
#r "nuget: System.Runtime, 4.3.1"

using System;
using System.Diagnostics;
using System.IO;

string aion2Path = @"C:\Program Files (x86)\NCSOFT\AION2_TW\Aion2\Binaries\Win64\Aion2.exe";

Console.WriteLine("=== Simple Aion2.exe Test ===\n");
Console.WriteLine($"Target: {aion2Path}\n");

if (!File.Exists(aion2Path))
{
    Console.WriteLine("ERROR: Aion2.exe not found!");
    Environment.Exit(1);
}

Console.WriteLine("Test 1: Basic process execution");
Console.WriteLine("----------------------------------------");

try
{
    var psi = new ProcessStartInfo
    {
        FileName = aion2Path,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = false
    };

    Console.WriteLine($"Starting: {aion2Path}");
    using (var proc = Process.Start(psi))
    {
        if (proc == null)
        {
            Console.WriteLine("ERROR: Failed to start process");
            Environment.Exit(1);
        }

        Console.WriteLine($"Process started (PID: {proc.Id})");
        Console.WriteLine("Waiting up to 10 seconds for crash...");

        if (!proc.WaitForExit(10000))
        {
            proc.Kill();
            Console.WriteLine("Process still running after 10s - killing");
        }
        else
        {
            Console.WriteLine($"Process exited with code: 0x{proc.ExitCode:X8}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    Environment.Exit(1);
}

Console.WriteLine("\n=== Test Complete ===");
