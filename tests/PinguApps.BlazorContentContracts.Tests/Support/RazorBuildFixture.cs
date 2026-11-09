using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

using Xunit;
using PinguApps.BlazorContentContracts.Analyzers;

namespace PinguApps.BlazorContentContracts.Tests.Support;


internal sealed class RazorBuildFixture : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "bcc-razor-" + Guid.NewGuid().ToString("N"));
    private string content = "";
    private int exitCode;
    private string output = "";

    public void GivenConsumer(string kind)
    {
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(RepositoryRoot(), "global.json"), Path.Combine(directory, "global.json"));
        content = kind switch
        {
            "invalid" => "<Slot>\n    <Bad />\n</Slot>",
            "valid" => "<Slot><Good /></Slot>",
            "empty" => "<Slot />",
            "comment" => "@* <Bad /> *@\n<Slot>\n    <Bad />\n</Slot>",
            "declarations" or "malformed declarations" => "<Slot />",
            _ => throw new ArgumentException(kind)
        };
        var packages = Environment.GetEnvironmentVariable("BCC_PACKAGES");
        var references = packages == null
            ? $"<Reference Include=\"PinguApps.BlazorContentContracts\" HintPath=\"{typeof(AllowedChildrenAttribute).Assembly.Location}\" /><Analyzer Include=\"{typeof(ContentContractAnalyzer).Assembly.Location}\" />"
            : "<PackageReference Include=\"PinguApps.BlazorContentContracts\" Version=\"1.0.0\" /><PackageReference Include=\"PinguApps.BlazorContentContracts.Analyzers\" Version=\"1.0.0\" />";
        File.WriteAllText(Path.Combine(directory, "Consumer.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ErrorLog>diagnostics.sarif</ErrorLog>
                <RestorePackagesPath>{{Path.Combine(directory, "packages")}}</RestorePackagesPath>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
              </PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                {{references}}
                <AdditionalFiles Include="**/*.razor" Exclude="bin/**;obj/**" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Usage.razor"), content);
        File.WriteAllText(Path.Combine(directory, "Slot.cs"), """
            using Microsoft.AspNetCore.Components;
            using PinguApps.BlazorContentContracts;

            public class Slot : ComponentBase
            {
                [Parameter, AllowedChildren(typeof(Good))]
                public RenderFragment ChildContent { get; set; }
            }
            """);
        if (kind is "declarations" or "malformed declarations")
        {
            var element = kind == "declarations" ? "div" : "<div>";
            File.WriteAllText(Path.Combine(directory, "Declared.cs"), $$"""
                using Microsoft.AspNetCore.Components;
                using PinguApps.BlazorContentContracts;

                [ProducesContent(HtmlElements = new[] { "{{element}}" }), ForwardsContent(nameof(Template))]
                public class Declared<T> : ComponentBase
                {
                    [Parameter, AllowedChildren(typeof(Declared<>), typeof(Declared<int>), HtmlElements = new[] { "span" })]
                    public RenderFragment<T> Template { get; set; }

                    [ProducesContent(AllowText = true)]
                    public RenderFragment Content() => throw new System.Exception("Must never execute during analysis");
                }
                """);
        }

        File.WriteAllText(Path.Combine(directory, "Good.cs"), """
            using Microsoft.AspNetCore.Components;

            public class Good : ComponentBase
            {
            }
            """);
        File.WriteAllText(Path.Combine(directory, "Bad.cs"), """
            using Microsoft.AspNetCore.Components;

            public class Bad : ComponentBase
            {
            }
            """);
    }

    public void WhenBuild()
    {
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            ?? throw new InvalidOperationException("DOTNET_HOST_PATH must identify the selected SDK executable.");
        var start = new ProcessStartInfo(dotnet)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("build");
        start.ArgumentList.Add("-v:q");
        var packages = Environment.GetEnvironmentVariable("BCC_PACKAGES");

        if (packages != null)
        {
            start.ArgumentList.Add("--source");
            start.ArgumentList.Add(packages);
        }

        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        exitCode = process.ExitCode;
    }

    public void ThenDiagnostics(string expected)
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        var diagnostics = results.Where(result => result.GetProperty("ruleId").GetString() == "BCC001").ToArray();

        if (expected == "none")
        {
            Assert.True(exitCode == 0, output);
            Assert.Empty(diagnostics);
        }
        else
        {
            Assert.True(exitCode != 0, output);
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("error", diagnostic.GetProperty("level").GetString());
            var location = diagnostic.GetProperty("locations")[0].GetProperty("resultFile");
            Assert.EndsWith("Usage.razor", location.GetProperty("uri").GetString());
            var region = location.GetProperty("region");
            var index = content.LastIndexOf("Bad", StringComparison.Ordinal);
            var line = content.Substring(0, index).Count(character => character == '\n') + 1;
            Assert.Equal(line, region.GetProperty("startLine").GetInt32());
            Assert.Equal(6, region.GetProperty("startColumn").GetInt32());
            Assert.Equal(9, region.GetProperty("endColumn").GetInt32());
        }

        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() == "AD0001");
    }

    public void ThenDeclarationDiagnostics(string expected)
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        var declarations = results.Where(result => result.GetProperty("ruleId").GetString() == "BCC002").ToArray();

        if (expected == "none")
        {
            Assert.True(exitCode == 0, output);
            Assert.Empty(declarations);
        }
        else
        {
            Assert.True(exitCode != 0, output);
            var diagnostic = Assert.Single(declarations);
            Assert.Contains("HtmlElements", diagnostic.GetProperty("message").GetString());
            Assert.EndsWith("Declared.cs", diagnostic.GetProperty("locations")[0].GetProperty("resultFile").GetProperty("uri").GetString());
        }

        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() == "AD0001");
    }

    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string path = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", ".."));
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }
}
