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
    public void GivenAdoption(string change)
    {
        GivenConsumer("empty");
        File.Delete(Path.Combine(directory, "Slot.cs"));
        var source = Path.Combine(RepositoryRoot(), "samples", "Adoption");

        foreach (var file in Directory.GetFiles(source).Where(file => file.EndsWith(".razor", StringComparison.Ordinal) || file.EndsWith(".cs", StringComparison.Ordinal)))
        {
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)), overwrite: true);
        }

        var project = Path.Combine(directory, "Consumer.csproj");
        File.WriteAllText(project, File.ReadAllText(project).Replace("<PropertyGroup>", "<PropertyGroup><RootNamespace>Adoption</RootNamespace>", StringComparison.Ordinal));

        if (change == "invalid descendant")
        {
            File.WriteAllText(Path.Combine(directory, "Usage.razor"), "<HtmlSlot><span><div /></span></HtmlSlot>");
        }
    }

    public void ThenOutputContains(string text)
    {
        Assert.Contains(text, output);
    }

    private readonly string directory = Path.Combine(Path.GetTempPath(), "bcc-razor-" + Guid.NewGuid().ToString("N"));
    private string content = "";
    private int exitCode;
    private string output = "";
    private bool editorLocations;

    public void GivenEditorLocations()
    {
        editorLocations = true;
    }

    public void WhenNormalBuildFollowsEditorAnalysis()
    {
        editorLocations = false;
        WhenBuild();
    }

    public void ThenEditorConfigurationPersists()
    {
        var config = Assert.Single(Directory.GetFiles(directory, "bcc-editor.globalconfig", SearchOption.AllDirectories));
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        var diagnostic = Assert.Single(results, result => result.GetProperty("ruleId").GetString() == "BCC005");
        var location = diagnostic.GetProperty("locations")[0].GetProperty("resultFile");

        Assert.EndsWith("Usage.razor", location.GetProperty("uri").GetString());
        Assert.Contains("build_property.BlazorContentContractsEditor = true", File.ReadAllText(config));
        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() == "AD0001");
    }

    public void ThenEditorLocations()
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        var diagnostic = Assert.Single(results, result => result.GetProperty("ruleId").GetString() == "BCC005");
        var location = diagnostic.GetProperty("locations")[0].GetProperty("resultFile");

        Assert.EndsWith(".g.cs", location.GetProperty("uri").GetString());
        var region = location.GetProperty("region");
        Assert.Equal(region.GetProperty("startLine").GetInt32(), region.GetProperty("endLine").GetInt32());
        Assert.Equal(3, region.GetProperty("endColumn").GetInt32() - region.GetProperty("startColumn").GetInt32());
        Assert.Contains("via Bad", diagnostic.GetProperty("message").GetString());
        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() == "AD0001");
    }

    public void GivenOutput(string body, string usage)
    {
        var typed = body.StartsWith("context =>", StringComparison.Ordinal);
        GivenComposition("@using PinguApps.BlazorContentContracts\n<Slot>" + usage + "</Slot>\n@code { [ProducesContent(typeof(Good))] private "
            + (typed ? "RenderFragment<int>" : "RenderFragment") + " Factory() => " + body + "; }");
    }

    public void GivenPublishedOutput(string permission)
    {
        GivenComposition("<Slot>@LibraryFactory.Create()<PublishedWrapper /></Slot>");
        var library = Path.Combine(directory, "Library");
        Directory.CreateDirectory(library);
        File.Move(Path.Combine(directory, "Good.cs"), Path.Combine(library, "Good.cs"));
        var framework = Environment.GetEnvironmentVariable("BCC_CONSUMER_FRAMEWORK") ?? "net11.0";
        File.WriteAllText(Path.Combine(library, "Library.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>{{framework}}</TargetFramework></PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                <Reference Include="PinguApps.BlazorContentContracts" HintPath="{{typeof(AllowedChildrenAttribute).Assembly.Location}}" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(library, "LibraryFactory.cs"), $$"""
            using Microsoft.AspNetCore.Components;
            using PinguApps.BlazorContentContracts;

            public static class LibraryFactory
            {
                [ProducesContent({{permission}})]
                public static RenderFragment Create() => throw new System.Exception();
            }
            """);
        File.WriteAllText(Path.Combine(library, "PublishedWrapper.cs"), $$"""
            using Microsoft.AspNetCore.Components;
            using PinguApps.BlazorContentContracts;

            [ProducesContent({{permission}})]
            public class PublishedWrapper : ComponentBase
            {
                protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
                {
                    throw new System.Exception();
                }
            }
            """);
        var project = Path.Combine(directory, "Consumer.csproj");
        File.WriteAllText(project, File.ReadAllText(project).Replace("</Project>", "<ItemGroup><Compile Remove=\"Library/**/*.cs\" /><ProjectReference Include=\"Library/Library.csproj\" /></ItemGroup></Project>", StringComparison.Ordinal));
    }

    public void GivenOutputSuppression(string body)
    {
        GivenComposition("<Slot>@Factory.Create() @Factory.Create()</Slot>");
        File.WriteAllText(Path.Combine(directory, "Factory.cs"), $$"""
            using Microsoft.AspNetCore.Components;
            using PinguApps.BlazorContentContracts;

            public static class Factory
            {
            #pragma warning disable BCC011
                [ProducesContent(typeof(Good))]
                public static RenderFragment Create() => builder =>
                {
                    {{body}}
                };
            #pragma warning restore BCC011
            }
            """);
    }

    public void GivenComponentOutput(string markup)
    {
        GivenComposition("<Slot><Wrapper /><Wrapper /></Slot>");

        if (markup == "inherited")
        {
            File.WriteAllText(Path.Combine(directory, "BaseWrapper.cs"), """
                using Microsoft.AspNetCore.Components;

                public class BaseWrapper : ComponentBase
                {
                    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
                    {
                        builder.AddContent(0, "forbidden");
                    }
                }
                """);
            File.WriteAllText(Path.Combine(directory, "Wrapper.cs"), """
                using PinguApps.BlazorContentContracts;

                [ProducesContent(typeof(Good))]
                public class Wrapper : BaseWrapper
                {
                }
                """);

            return;
        }

        File.WriteAllText(Path.Combine(directory, "Wrapper.razor"), "@using Microsoft.AspNetCore.Components\n@using PinguApps.BlazorContentContracts\n@attribute [ProducesContent(typeof(Good))]\n" + markup);
    }

    public void GivenCombinedOutput(string permissions, string child)
    {
        GivenComposition("<Slot><Wrapper>" + child + "</Wrapper></Slot>");
        var slot = Path.Combine(directory, "Slot.cs");
        File.WriteAllText(slot, File.ReadAllText(slot).Replace("AllowedChildren(typeof(Good))", "AllowedChildren(" + permissions + ")", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(directory, "Wrapper.razor"), """
            @using Microsoft.AspNetCore.Components
            @using PinguApps.BlazorContentContracts
            @attribute [ProducesContent(typeof(Good))]
            @attribute [ForwardsContent(nameof(ChildContent))]
            <Good />
            @ChildContent
            @code {
                [Parameter] public RenderFragment ChildContent { get; set; }
            }
            """);
    }

    public void ThenForwarding(string rule, int count)
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray()
            .Where(result => !result.TryGetProperty("suppressionStates", out var states)
                || !states.EnumerateArray().Any(state => state.GetString() == "suppressedInSource")).ToArray();

        Assert.Equal(count, results.Count(result => result.GetProperty("ruleId").GetString() == rule));

        if (rule == "BCC009")
        {
            Assert.All(results.Where(result => result.GetProperty("ruleId").GetString() == rule), result =>
                Assert.Contains("declares output not allowed", result.GetProperty("message").GetString()));
        }

        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() == "AD0001"
            || result.GetProperty("ruleId").GetString()!.StartsWith("CS", StringComparison.Ordinal));
        Assert.All(results.Where(result => result.GetProperty("ruleId").GetString()!.StartsWith("BCC", StringComparison.Ordinal)), result =>
            Assert.True(result.GetProperty("ruleId").GetString() == rule
                || rule == "BCC003" && result.GetProperty("ruleId").GetString() == "BCC005", output));
        Assert.Equal(results.Any(result => result.GetProperty("level").GetString() == "error"), exitCode != 0);
    }

    public void GivenForwarding(string permissions, string usage)
    {
        GivenComposition(usage);
        File.WriteAllText(Path.Combine(directory, "DerivedGood.cs"), "public class DerivedGood : Good\n{\n}\n");
        File.WriteAllText(Path.Combine(directory, "Row.cs"), "public class Row<T> : Microsoft.AspNetCore.Components.ComponentBase\n{\n}\n");
        var parts = permissions.Split(" => ", StringSplitOptions.None);

        if (parts.Length == 2)
        {
            permissions = parts[0];
            File.WriteAllText(Path.Combine(directory, "Slot.cs"), "using Microsoft.AspNetCore.Components; using PinguApps.BlazorContentContracts; public class Slot : ComponentBase { [Parameter, AllowedChildren(" + parts[1] + ")] public RenderFragment ChildContent { get; set; } }");
        }

        var typed = permissions == "typed";
        var inherited = permissions is "inherited" or "conflict";
        var declaration = permissions == "opaque" ? "" : "[AllowedChildren(" + (typed || inherited ? "typeof(Good)" : permissions) + ")]";
        var fragment = typed ? "RenderFragment<int>" : "RenderFragment";
        var markup = typed ? "<Slot><Template>@ChildContent(1)</Template></Slot>" : "<Slot ChildContent=\"@ChildContent\" />";

        if (inherited)
        {
            File.WriteAllText(Path.Combine(directory, "BaseWrapper.cs"), "using Microsoft.AspNetCore.Components; using PinguApps.BlazorContentContracts; public class BaseWrapper : ComponentBase { [Parameter, AllowedChildren(typeof(Good))] public RenderFragment ChildContent { get; set; } }");
        }

        var property = inherited ? permissions == "conflict"
            ? "[Parameter, AllowedChildren(typeof(Good), typeof(Bad))] public new RenderFragment ChildContent { get; set; }" : ""
            : "[Parameter] " + declaration + " public " + fragment + " ChildContent { get; set; }";
        File.WriteAllText(Path.Combine(directory, "Wrapper.razor"), "@using Microsoft.AspNetCore.Components\n@using PinguApps.BlazorContentContracts\n"
            + (inherited ? "@inherits BaseWrapper\n" : "") + markup + "\n@code { " + property + " }");
    }

    public void GivenWrapper(string markup, string usage)
    {
        GivenComposition("<Slot>" + usage + "</Slot>");
        File.WriteAllText(Path.Combine(directory, "GenericWrapper.razor"), "@using Microsoft.AspNetCore.Components\n@typeparam TItem\n@ChildContent\n@code { [Parameter] public TItem Value { get; set; } [Parameter] public RenderFragment ChildContent { get; set; } }");
        File.WriteAllText(Path.Combine(directory, "Inner.razor"), "@using Microsoft.AspNetCore.Components\n@ChildContent\n@code { [Parameter] public RenderFragment ChildContent { get; set; } }");
        File.WriteAllText(Path.Combine(directory, "Wrapper.razor"), "@using Microsoft.AspNetCore.Components\n" + markup + "\n" + """
            @code {
                [Parameter] public RenderFragment Header { get; set; }
                [Parameter] public RenderFragment Body { get; set; }
                [Parameter] public RenderFragment Footer { get; set; }
                [Parameter] public RenderFragment<int> Template { get; set; }
                public Wrapper() { throw new System.Exception("Never construct"); }
                protected override void OnInitialized() { throw new System.Exception("Never run lifecycle"); }
                private RenderFragment Factory() => throw new System.Exception("Never execute");
            }
            """);
    }

    public void GivenBoundedOutput(string kind)
    {
        GivenWrapper("<Inner />", "<Wrapper /><span />");

        if (kind == "fragment cycle")
        {
            GivenComposition("<Slot>@First()<span /></Slot>\n@code { private RenderFragment First() => Second(); private RenderFragment Second() => First(); }");
            return;
        }

        if (kind == "contracts")
        {
            File.WriteAllText(Path.Combine(directory, "Wrapper.razor"), "@using Microsoft.AspNetCore.Components\n@Body\n@code { [Parameter] public RenderFragment Body { get; set; } }");
            File.WriteAllText(Path.Combine(directory, "Usage.razor"), "<Slot><ChildContent><Wrapper><Body><Good /></Body></Wrapper></ChildContent><Header><Wrapper><Body><Good /></Body></Wrapper></Header></Slot>");
            return;
        }

        if (kind == "cycle")
        {
            File.WriteAllText(Path.Combine(directory, "Inner.razor"), "<Wrapper />");
        }
        else if (kind == "work limit")
        {
            File.WriteAllText(Path.Combine(directory, "Inner.razor"), string.Concat(Enumerable.Repeat("<Leaf />", 4200)));
            File.WriteAllText(Path.Combine(directory, "Leaf.razor"), "<Good />");
        }
        else
        {
            File.WriteAllText(Path.Combine(directory, "Inner.razor"), "<Level0 />");

            for (var index = 0; index < 70; index++)
            {
                File.WriteAllText(Path.Combine(directory, "Level" + index + ".razor"),
                    index == 69 ? "<Good />" : "<Level" + (index + 1) + " />");
            }
        }
    }

    public void ThenBoundedOutput(string rule)
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        var diagnostic = Assert.Single(results, result => result.GetProperty("ruleId").GetString() == rule);

        Assert.Equal("warning", diagnostic.GetProperty("level").GetString());
        Assert.Contains("declare an output contract", diagnostic.GetProperty("message").GetString());
        Assert.Single(results, result => result.GetProperty("ruleId").GetString() == "BCC005");
        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() == "AD0001");
        Assert.EndsWith("Usage.razor", diagnostic.GetProperty("locations")[0].GetProperty("resultFile").GetProperty("uri").GetString());

        if (rule == "BCC012")
        {
            Assert.Contains(" -> ", diagnostic.GetProperty("message").GetString());
        }
    }

    public void ThenWrapperPath()
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var diagnostic = Assert.Single(sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray(),
            result => result.GetProperty("ruleId").GetString() == "BCC005");
        Assert.Contains("Wrapper", diagnostic.GetProperty("message").GetString());
        Assert.EndsWith("Usage.razor", diagnostic.GetProperty("locations")[0].GetProperty("resultFile").GetProperty("uri").GetString());
    }

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
                protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
                {
                    builder.AddContent(0, "component output");
                }

            }
            """);
        File.WriteAllText(Path.Combine(directory, "Bad.cs"), """
            using Microsoft.AspNetCore.Components;

            public class Bad : ComponentBase
            {
                protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
                {
                    builder.AddContent(0, "invalid output");
                }
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
                protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
                {
                    builder.AddContent(0, "component output");
                }

                [Parameter] public RenderFragment ChildContent { get; set; }
                [Parameter] public EventCallback Activated { get; set; }
            }
            """);
    }

    public void GivenSeverity(string severity)
    {
        File.WriteAllText(Path.Combine(directory, ".globalconfig"), "is_global = true\ndotnet_diagnostic.BCC006.severity = " + severity
            + "\ndotnet_diagnostic.BCC004.severity = " + severity);
    }

    public void GivenRendering()
    {
        const string comment = "@* bcc-suppress-next BCC004: trusted markup *@";
        const string usage = "@using Microsoft.AspNetCore.Components\n<Slot>" + comment + "@((MarkupString)\"<span>Trusted</span>\")</Slot>";
        GivenComposition(usage);
        File.WriteAllText(Path.Combine(directory, "Baseline.razor"), usage.Replace(comment, "", StringComparison.Ordinal));
        var project = Path.Combine(directory, "Consumer.csproj");
        File.WriteAllText(project, File.ReadAllText(project).Replace("<PropertyGroup>", "<PropertyGroup><OutputType>Exe</OutputType>", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(directory, "Slot.cs"), """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Rendering;
            using PinguApps.BlazorContentContracts;

            public class Slot : ComponentBase
            {
                [Parameter, AllowedChildren(typeof(Good))]
                public RenderFragment ChildContent { get; set; }

                protected override void BuildRenderTree(RenderTreeBuilder builder)
                {
                    builder.AddContent(0, ChildContent);
                }
            }
            """);
        File.WriteAllText(Path.Combine(directory, "Program.cs"), """
            using System;
            using System.Linq;
            using System.Reflection;
            using Microsoft.AspNetCore.Components.Web;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Logging;

            using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var types = Assembly.GetExecutingAssembly().GetTypes();
            var output = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var suppressed = await renderer.RenderComponentAsync(types.Single(type => type.Name == "Usage"), Microsoft.AspNetCore.Components.ParameterView.Empty);
                var baseline = await renderer.RenderComponentAsync(types.Single(type => type.Name == "Baseline"), Microsoft.AspNetCore.Components.ParameterView.Empty);
                return (suppressed.ToHtmlString(), baseline.ToHtmlString());
            });

            if (output.Item1 != "<span>Trusted</span>" || output.Item1 != output.Item2)
            {
                throw new Exception("Suppression changed rendered output: " + output);
            }

            Console.WriteLine("Identical rendered markup: " + output.Item1);
            """);
    }

    public void ThenRendering()
    {
        Assert.True(exitCode == 0, output);
        var dotnet = Environment.GetEnvironmentVariable("BCC_CONSUMER_DOTNET") ?? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")!;
        var start = new ProcessStartInfo(dotnet)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(directory, "bin", "Debug", Environment.GetEnvironmentVariable("BCC_CONSUMER_FRAMEWORK") ?? "net11.0", "Consumer.dll"));
        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
        Assert.Contains("Identical rendered markup: <span>Trusted</span>", stdout.GetAwaiter().GetResult());
    }

    public void ThenSuppressions(string rules)
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        var diagnostics = results.Where(result => result.GetProperty("ruleId").GetString()!.StartsWith("BCC", StringComparison.Ordinal)).ToArray();
        var expected = rules == "none" ? Array.Empty<string>() : rules.Split(',');

        Assert.Equal(expected.OrderBy(rule => rule), diagnostics.Select(result => result.GetProperty("ruleId").GetString()).OrderBy(rule => rule));
        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() == "AD0001");
        Assert.All(diagnostics, result => Assert.EndsWith("Usage.razor", result.GetProperty("locations")[0].GetProperty("resultFile").GetProperty("uri").GetString()));
        Assert.Equal(!expected.Any(rule => rule is "BCC001" or "BCC004" or "BCC005" or "BCC006"), exitCode == 0);
    }

    public void ThenUnknown(int unknown, int invalid, string severity = "warning")
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        var unresolved = results.Where(result => result.GetProperty("ruleId").GetString() == "BCC006").ToArray();

        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() is "BCC012" or "BCC013");
        Assert.Equal(unknown, unresolved.Length);
        Assert.True(invalid == results.Count(result => result.GetProperty("ruleId").GetString() is "BCC001" or "BCC005"), output);
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
                protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
                {
                    builder.AddContent(0, "component output");
                }

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
                protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
                {
                    builder.AddContent(0, "component output");
                }

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

        foreach (var diagnostic in results.Where(result => result.GetProperty("ruleId").GetString() == "BCC005"))
        {
            var location = diagnostic.GetProperty("locations")[0].GetProperty("resultFile");
            Assert.EndsWith("Usage.razor", location.GetProperty("uri").GetString());
            Assert.Equal(2, location.GetProperty("region").GetProperty("startLine").GetInt32());
        }
    }

    public void WhenBuild(bool analysis = true)
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

        if (!analysis)
        {
            start.ArgumentList.Add("-p:RunAnalyzers=false");
        }

        if (editorLocations)
        {
            start.ArgumentList.Add("-p:DesignTimeBuild=true");
            start.ArgumentList.Add("-p:EmitCompilerGeneratedFiles=false");
            start.ArgumentList.Add("-p:CompilerGeneratedFilesOutputPath=");
        }

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

        if (!process.WaitForExit(90000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Razor consumer build exceeded 90 seconds.");
        }

        output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        exitCode = process.ExitCode;
        Assert.True(File.Exists(Path.Combine(directory, "diagnostics.sarif")), output);
    }

    public void VerifyEditInvalidation(string kind)
    {
        if (kind == "referenced summary")
        {
            GivenPublishedOutput("typeof(Good)");
            File.WriteAllText(Path.Combine(directory, "Library", "Other.cs"),
                "public class Other : Microsoft.AspNetCore.Components.ComponentBase { }");
        }
        else
        {
            GivenWrapper("<Good />", "<Wrapper />");
        }

        if (kind == "assignment")
        {
            File.WriteAllText(Path.Combine(directory, "Usage.razor"),
                "<Slot>@Item</Slot>\n@code { private RenderFragment Item => builder => { builder.OpenComponent<Good>(0); builder.CloseComponent(); }; }");
        }

        WhenBuild();
        ThenUnknown(0, 0);
        var path = Path.Combine(directory, kind switch
        {
            "contract" => "Slot.cs",
            "assignment" => "Usage.razor",
            "referenced summary" => "Library/LibraryFactory.cs",
            _ => "Wrapper.razor"
        });
        var original = File.ReadAllText(path);
        var changed = kind switch
        {
            "contract" => original.Replace("AllowedChildren(typeof(Good))", "AllowedChildren(typeof(Bad))", StringComparison.Ordinal),
            "assignment" => original.Replace("builder.OpenComponent<Good>(0); builder.CloseComponent();", "builder.OpenElement(0, \"span\"); builder.CloseElement();", StringComparison.Ordinal),
            "referenced summary" => original.Replace("typeof(Good)", "typeof(Other)", StringComparison.Ordinal),
            _ => "<span />"
        };
        File.WriteAllText(path, changed);

        WhenBuild();
        Assert.True(exitCode != 0, output);
        Assert.Contains(kind == "referenced summary" ? "BCC009" : "BCC005", output);
        Assert.DoesNotContain("AD0001", output);
        File.WriteAllText(path, original);

        WhenBuild();
        ThenUnknown(0, 0);
    }

    public void MeasureBoundedBuild(string kind)
    {
        GivenBoundedOutput(kind);

        foreach (var analysis in new[] { true, false, true, false })
        {
            // Force compilation while retaining restored dependencies and generated inputs.
            var usage = Path.Combine(directory, "Usage.razor");
            File.AppendAllText(usage, "\n@* measurement rebuild *@");
            var timer = Stopwatch.StartNew();
            WhenBuild(analysis);
            timer.Stop();
            Console.WriteLine($"BCC measurement: {kind}, analysis={analysis}, elapsed_ms={timer.ElapsedMilliseconds}, exit={exitCode}, framework={Environment.GetEnvironmentVariable("BCC_CONSUMER_FRAMEWORK") ?? "net11.0"}, runtime={Environment.Version}, os={Environment.OSVersion}, processors={Environment.ProcessorCount}");

            if (analysis)
            {
                ThenBoundedOutput("BCC013");
            }
            else
            {
                Assert.Equal(0, exitCode);
            }
        }
    }

    public void ThenDiagnostics(string expected)
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        var diagnostics = results.Where(result => result.GetProperty("ruleId").GetString() == "BCC005").ToArray();

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
