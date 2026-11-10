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
        var net10 = Environment.GetEnvironmentVariable("BCC_CONSUMER_FRAMEWORK") == "net10.0";
        var sdk = net10 ? "10.0.403" : "11.0.100";
        File.WriteAllText(Path.Combine(directory, "global.json"), JsonSerializer.Serialize(new { sdk = new { version = sdk, rollForward = "latestPatch" } }));
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
                <TargetFramework>{{Environment.GetEnvironmentVariable("BCC_CONSUMER_FRAMEWORK") ?? "net11.0"}}</TargetFramework>
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

    public void GivenMarkup(string html, string text, string usage)
    {
        GivenConsumer("valid");
        content = "<Slot>" + usage + "</Slot>";
        File.WriteAllText(Path.Combine(directory, "Usage.razor"), content);
        File.WriteAllText(Path.Combine(directory, "Slot.cs"), $$"""
            using Microsoft.AspNetCore.Components;
            using PinguApps.BlazorContentContracts;

            public class Slot : ComponentBase
            {
                [Parameter, AllowedChildren(HtmlElements = new[] { "{{html}}" }, AllowText = {{text}})]
                public RenderFragment ChildContent { get; set; }
            }
            """);
    }

    public void GivenComposition(string usage)
    {
        GivenConsumer("valid");
        File.WriteAllText(Path.Combine(directory, "Usage.razor"), "@using Microsoft.AspNetCore.Components\n" + usage.Replace("@code", "\n@code", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(directory, "Slot.cs"), """
            using Microsoft.AspNetCore.Components;
            using PinguApps.BlazorContentContracts;

            public class Slot : ComponentBase
            {
                [Parameter, AllowedChildren(typeof(Good))]
                public RenderFragment ChildContent { get; set; }

                [Parameter, AllowedChildren(typeof(Bad))]
                public RenderFragment Header { get; set; }

                [Parameter, AllowedChildren(typeof(Good))]
                public RenderFragment<int> Template { get; set; }
            }
            """);
        File.WriteAllText(Path.Combine(directory, "Good.cs"), """
            using Microsoft.AspNetCore.Components;

            public class Good : ComponentBase
            {
                [Parameter] public RenderFragment ChildContent { get; set; }
                [Parameter] public EventCallback Activated { get; set; }
            }
            """);
    }

    public void GivenSeverity(string severity)
    {
        File.WriteAllText(Path.Combine(directory, ".globalconfig"), "is_global = true\ndotnet_diagnostic.BCC006.severity = " + severity);
    }

    public void ThenUnknown(int unknown, int invalid, string severity = "warning")
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        var unresolved = results.Where(result => result.GetProperty("ruleId").GetString() == "BCC006").ToArray();

        Assert.Equal(unknown, unresolved.Length);
        Assert.Equal(invalid, results.Count(result => result.GetProperty("ruleId").GetString() is "BCC001" or "BCC005"));
        Assert.All(unresolved, result =>
        {
            Assert.Equal(severity, result.GetProperty("level").GetString());
            Assert.Contains("cannot be analysed:", result.GetProperty("message").GetString());
        });
        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() == "AD0001");
        Assert.True((invalid == 0 && severity == "warning") == (exitCode == 0), output);
    }

    public void ThenMarkupDiagnostics(string rule, int count)
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        var diagnostics = results.Where(result => result.GetProperty("ruleId").GetString()!.StartsWith("BCC", StringComparison.Ordinal)).ToArray();
        Assert.Equal(count, diagnostics.Length);
        Assert.All(diagnostics, result => Assert.Equal(rule, result.GetProperty("ruleId").GetString()));
        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() == "AD0001");
        Assert.True((rule == "BCC004" || count == 0) == (exitCode == 0), output);
    }

    public void GivenFamily(string allowance, string usage, string inheritance)
    {
        GivenConsumer("valid");
        content = "@using Families\n<Slot>" + usage + "</Slot>";
        File.WriteAllText(Path.Combine(directory, "Usage.razor"), content);
        File.WriteAllText(Path.Combine(directory, "SlotBase.cs"), $$"""
            using Microsoft.AspNetCore.Components;
            using PinguApps.BlazorContentContracts;

            public class SlotBase : ComponentBase
            {
                [Parameter, AllowedChildren(typeof({{allowance}}))]
                public virtual RenderFragment ChildContent { get; set; }
            }
            """);
        File.WriteAllText(Path.Combine(directory, "Slot.cs"), $$"""
            using Microsoft.AspNetCore.Components;
            using PinguApps.BlazorContentContracts;

            public class Slot : SlotBase
            {
                {{inheritance}}
            }
            """);
        File.WriteAllText(Path.Combine(directory, "Family.cs"), """
            using Microsoft.AspNetCore.Components;

            namespace Families;

            public class Family<T> : ComponentBase
            {
                [Parameter] public T Value { get; set; }
                [Parameter] public RenderFragment ChildContent { get; set; }
                [Parameter] public EventCallback Activated { get; set; }
            }
            """);
        File.WriteAllText(Path.Combine(directory, "Derived.cs"), """
            namespace Families;

            public class Derived : Family<int>
            {
            }
            """);
        Directory.CreateDirectory(Path.Combine(directory, "Other"));
        File.WriteAllText(Path.Combine(directory, "Other", "Family.cs"), """
            using Microsoft.AspNetCore.Components;

            namespace Other;

            public class Family<T> : ComponentBase
            {
            }
            """);
        File.WriteAllText(Path.Combine(directory, "_Imports.razor"), "@using Microsoft.AspNetCore.Components");
    }

    public void ThenFamilyDiagnostics(string rule, int count)
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(count, results.Count(result => result.GetProperty("ruleId").GetString() == rule));
        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() == "AD0001");
        Assert.True((count == 0) == (exitCode == 0), output);

        foreach (var diagnostic in results.Where(result => result.GetProperty("ruleId").GetString() == "BCC001"))
        {
            var location = diagnostic.GetProperty("locations")[0].GetProperty("resultFile");
            Assert.EndsWith("Usage.razor", location.GetProperty("uri").GetString());
            Assert.Equal(2, location.GetProperty("region").GetProperty("startLine").GetInt32());
        }
    }

    public void WhenBuild()
    {
        var dotnet = Environment.GetEnvironmentVariable("BCC_CONSUMER_DOTNET") ?? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
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
        start.ArgumentList.Add("--disable-build-servers");
        var packages = Environment.GetEnvironmentVariable("BCC_PACKAGES");

        if (packages != null)
        {
            start.ArgumentList.Add("--source");
            start.ArgumentList.Add(packages);
        }

        start.Environment["DOTNET_HOST_PATH"] = dotnet;
        start.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(dotnet)!;
        // A file-based verification app can carry its SDK resolver into child processes.
        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("DOTNET_MSBUILD_SDK_RESOLVER_", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            start.Environment.Remove(name);
        }

        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        exitCode = process.ExitCode;
        Assert.True(File.Exists(Path.Combine(directory, "diagnostics.sarif")), output);
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
