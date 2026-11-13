using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal static class ReleaseScript
{
    public static (int ExitCode, string Output) Run(string script, params string[] arguments)
    {
        var executable = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh"))
            .First(File.Exists);
        var root = Environment.GetEnvironmentVariable("BCC_REPOSITORY_ROOT")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var start = new ProcessStartInfo(Path.GetFullPath(executable))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in new[] { "-NoProfile", "-File", Path.Combine(root, "scripts", script) }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        errors.GetAwaiter().GetResult();
        return (process.ExitCode, output);
    }
}
