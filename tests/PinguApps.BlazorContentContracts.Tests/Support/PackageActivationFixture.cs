using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal sealed class PackageActivationFixture : IDisposable
{
    private const string Package = "PinguApps.BlazorContentContracts";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "bcc-activation-" + Guid.NewGuid().ToString("N"));
    private string mode = "";
    private string output = "";
    private int exitCode;

    public void Create(string graph)
    {
        mode = graph;
        Directory.CreateDirectory(directory);
        var packages = Environment.GetEnvironmentVariable("BCC_PACKAGES")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/packages"));
        File.WriteAllText(Path.Combine(directory, "NuGet.Config"),
            $"<configuration><packageSources><clear /><add key=\"local\" value=\"{packages}\" /><add key=\"graph\" value=\"{directory}\" /></packageSources></configuration>");
        var framework = Environment.GetEnvironmentVariable("BCC_CONSUMER_FRAMEWORK") ?? "net11.0";
        File.WriteAllText(Path.Combine(directory, "global.json"),
            framework == "net10.0" && mode != "multi" ? """{"sdk":{"version":"10.0.403","rollForward":"latestPatch"}}"""
                : """{"sdk":{"version":"11.0.100","rollForward":"latestPatch"}}""");
        var reference = $"<PackageReference Include=\"{Package}\" Version=\"1.0.0\" />";

        if (mode is "chain" or "project" or "private")
        {
            Project("Components", framework, reference.Replace(" />", mode == "private" ? " PrivateAssets=\"all\" />" : " />"));
            File.WriteAllText(Path.Combine(directory, "Components", "Marker.cs"), "public class Marker { }");
            Run("Components", "pack", "-o", directory);
            Assert.True(exitCode == 0, output);
            Project("Wrappers", framework, "<PackageReference Include=\"Components\" Version=\"1.0.0\" />");
            File.WriteAllText(Path.Combine(directory, "Wrappers", "Wrapper.cs"), "public class Wrapper { }");
            Run("Wrappers", "pack", "-o", directory);
            Assert.True(exitCode == 0, output);
            reference = mode == "project" ? "<ProjectReference Include=\"../Components/Components.csproj\" />"
                : "<PackageReference Include=\"Wrappers\" Version=\"1.0.0\" />";
        }

        if (mode == "central")
        {
            File.WriteAllText(Path.Combine(directory, "Directory.Packages.props"),
                $"<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup><PackageVersion Include=\"{Package}\" Version=\"1.0.0\" /></ItemGroup></Project>");
            reference = reference.Replace(" Version=\"1.0.0\"", "");
        }

        if (mode is "duplicate" or "version")
        {
            reference += $"<PackageReference Include=\"{Package}.Analyzers\" Version=\"{(mode == "version" ? "1.0.1" : "1.0.0")}\" PrivateAssets=\"all\" />";
        }

        Project("Consumer", mode == "multi" ? "net10.0;net11.0" : framework, reference);
        File.WriteAllText(Path.Combine(directory, "Consumer", "Invalid.razor"),
            "<Slot>forbidden</Slot>");
        File.WriteAllText(Path.Combine(directory, "Consumer", "Slot.cs"), """
            using Microsoft.AspNetCore.Components;
            public class Slot : ComponentBase
            {
                [Parameter, PinguApps.BlazorContentContracts.AllowedChildren(typeof(Slot))]
                public RenderFragment ChildContent { get; set; }
            }
            """);

        if (mode == "private")
        {
            File.Delete(Path.Combine(directory, "Consumer", "Slot.cs"));
            File.Delete(Path.Combine(directory, "Consumer", "Invalid.razor"));
        }
    }

    private void Project(string name, string framework, string references)
    {
        var path = Directory.CreateDirectory(Path.Combine(directory, name)).FullName;
        File.WriteAllText(Path.Combine(path, name + ".csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFrameworks>{{framework}}</TargetFrameworks>
                <RestorePackagesPath>{{directory}}/packages</RestorePackagesPath>
                <BlazorContentContractsEnabled>{{(mode == "opt-out" ? "false" : "true")}}</BlazorContentContractsEnabled>
              </PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                {{references}}
              </ItemGroup>
              <Target Name="RecordActivation" BeforeTargets="CoreCompile" DependsOnTargets="BlazorContentContractsConfigureAnalysis" Condition="'$(BlazorContentContractsEnabled)' != '' and '$(TargetFramework)' != ''">
                <WriteLinesToFile File="analyzers-$(TargetFramework).txt" Lines="@(Analyzer)" Overwrite="true" />
              </Target>
            </Project>
            """.Replace("DependsOnTargets=\"BlazorContentContractsConfigureAnalysis\"", mode == "private" ? "" : "DependsOnTargets=\"BlazorContentContractsConfigureAnalysis\""));
    }

    public void Build()
    {
        Run("Consumer", "restore");
        Assert.True(exitCode == 0, output);
        Run("Consumer", "build", "--no-restore");
    }

    public void Verify(bool enabled)
    {
        Assert.True(enabled == (exitCode != 0), output);
        Assert.True(enabled == output.Contains("BCC005", StringComparison.Ordinal), output);
        Assert.DoesNotContain("AD0001", output);
        Assert.DoesNotContain("CS8032", output);

        foreach (var record in Directory.GetFiles(Path.Combine(directory, "Consumer"), "analyzers-*.txt"))
        {
            if (mode == "version")
            {
                Assert.Contains(File.ReadAllLines(record), line => Path.GetFullPath(line).Replace('\\', '/').Contains("/1.0.1/analyzers/", StringComparison.Ordinal));
            }

            Assert.Equal(enabled ? 1 : 0, File.ReadAllLines(record).Count(line => line.EndsWith(Package + ".Analyzers.dll", StringComparison.Ordinal)));
        }

        Assert.NotEmpty(Directory.GetFiles(Path.Combine(directory, "Consumer"), "analyzers-*.txt"));
    }

    private void Run(string project, params string[] arguments)
    {
        var dotnet = Environment.GetEnvironmentVariable("BCC_CONSUMER_DOTNET")
            ?? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? throw new InvalidOperationException("Set DOTNET_HOST_PATH.");
        var start = new ProcessStartInfo(dotnet)
        {
            WorkingDirectory = Path.Combine(directory, project),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments.Concat(new[] { "--disable-build-servers", "-v:q" }))
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(120000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Package activation build exceeded two minutes.");
        }

        output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        exitCode = process.ExitCode;
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }
}
