using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using Reqnroll;

namespace PinguApps.BlazorContentContracts.Tests.Support;

[Binding]
public sealed class VerificationMetrics
{
    private static readonly Stopwatch Timer = Stopwatch.StartNew();
    private static int compilations;
    private static int sdkCommands;

    internal static void Compilation()
    {
        Interlocked.Increment(ref compilations);
    }

    internal static void SdkCommand()
    {
        Interlocked.Increment(ref sdkCommands);
    }

    [AfterTestRun]
    public static void Report()
    {
        var metrics = new { elapsedMs = Timer.ElapsedMilliseconds, compilerInvocations = compilations, sdkCommands };
        Console.WriteLine("BCC verification metrics: " + JsonSerializer.Serialize(metrics));
        var path = Environment.GetEnvironmentVariable("BCC_TEST_METRICS");

        if (!string.IsNullOrEmpty(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(metrics));
        }
    }
}
