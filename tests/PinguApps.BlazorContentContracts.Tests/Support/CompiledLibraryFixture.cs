using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal sealed class CompiledLibraryFixture : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "bcc-compiled-" + Guid.NewGuid().ToString("N"));
    private string output = "";
    private int exitCode;
    private string kind = "";

    public void Create(string wrapper)
    {
        kind = wrapper;
        Directory.CreateDirectory(directory);

        if (Environment.GetEnvironmentVariable("BCC_CONSUMER_FRAMEWORK") == "net10.0")
        {
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"10.0.403\",\"rollForward\":\"latestPatch\",\"allowPrerelease\":false}}");
        }

        var packages = Environment.GetEnvironmentVariable("BCC_PACKAGES")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/packages"));
        Assert.True(Directory.Exists(packages), "Pack the repository packages before running compiled-library scenarios.");
        File.WriteAllText(Path.Combine(directory, "NuGet.Config"), $"<configuration><packageSources><clear /><add key=\"local\" value=\"{packages}\" /><add key=\"libraries\" value=\"{directory}\" /></packageSources></configuration>");
        Project("Menu", "");
        File.WriteAllText(Path.Combine(directory, "Menu", "Menu.cs"), """
            using Microsoft.AspNetCore.Components;
            using PinguApps.BlazorContentContracts;

            public class Menu : ComponentBase
            {
                [Parameter, AllowedChildren(typeof(Good))]
                public RenderFragment ChildContent { get; set; }
            }
            """);
        File.WriteAllText(Path.Combine(directory, "Menu", "Good.cs"), "public class Good : Microsoft.AspNetCore.Components.ComponentBase\n{\n}\n");

        if (wrapper == "obsolete")
        {
            File.WriteAllText(Path.Combine(directory, "Menu", "Good.cs"), "[System.Obsolete(\"Use the replacement component\")]\npublic class Good : Microsoft.AspNetCore.Components.ComponentBase\n{\n}\n");
        }

        File.WriteAllText(Path.Combine(directory, "Menu", "Bad.cs"), "public class Bad : Microsoft.AspNetCore.Components.ComponentBase\n{\n}\n");
        Run("Menu", "pack", "-o", directory);
        Assert.Equal(0, exitCode);
        Project("Wrappers", "<PackageReference Include=\"Menu\" Version=\"1.0.0\" />");

        if (wrapper.StartsWith("external", StringComparison.Ordinal))
        {
            File.WriteAllText(Path.Combine(directory, "Wrappers", "Wrapper.cs"), """
                using Microsoft.AspNetCore.Components;
                namespace Published;

                public class Wrapper : ComponentBase
                {
                    [Parameter] public RenderFragment ChildContent { get; set; }

                    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
                    {
                        builder.AddContent(0, ChildContent);
                    }
                }
                """);
            var project = Path.Combine(directory, "Wrappers", "Wrappers.csproj");
            File.WriteAllText(project, File.ReadAllText(project).Replace("<PackageReference Include=\"PinguApps.BlazorContentContracts.Analyzers\" Version=\"1.0.0\" PrivateAssets=\"all\" />", ""));

            if (wrapper == "external generic")
            {
                var source = Path.Combine(directory, "Wrappers", "Wrapper.cs");
                File.WriteAllText(source, File.ReadAllText(source).Replace("class Wrapper :", "class Wrapper<TItem> :"));
            }

            if (wrapper == "external empty")
            {
                File.WriteAllText(Path.Combine(directory, "Wrappers", "Wrapper.cs"), """
                    namespace Published;

                    public class Wrapper : Microsoft.AspNetCore.Components.ComponentBase
                    {
                        [Microsoft.AspNetCore.Components.Parameter]
                        public Microsoft.AspNetCore.Components.RenderFragment ChildContent { get; set; }
                    }
                    """);
            }

            if (wrapper == "external precedence")
            {
                File.WriteAllText(Path.Combine(directory, "Wrappers", "Conflict.cs"), "namespace Published;\n[PinguApps.BlazorContentContracts.ProducesContent(AllowText = true)]\npublic class Conflict : Wrapper\n{\n}\n");
            }

            if (wrapper == "external default")
            {
                var source = Path.Combine(directory, "Wrappers", "Wrapper.cs");
                File.WriteAllText(source, File.ReadAllText(source).Replace("builder.AddContent(0, ChildContent);", """
                    if (ChildContent == null)
                    {
                        builder.OpenElement(0, "div");
                        builder.CloseElement();
                    }
                    else
                    {
                        builder.AddContent(0, ChildContent);
                    }
                    """));
            }
        }
        else if (wrapper is "razor" or "custom path" or "generic forwarding" or "typed forwarding" or "invalid forwarding")
        {
            var generic = wrapper is not ("razor" or "custom path");
            File.WriteAllText(Path.Combine(directory, "Wrappers", "Wrapper.razor"),
                "@namespace Published\n@using Microsoft.AspNetCore.Components\n"
                + (generic ? "@typeparam TItem\n" : "") + "<Good />\n"
                + (generic ? wrapper == "typed forwarding"
                    ? "@ChildContent(default(TItem))\n@code { [Parameter] public RenderFragment<TItem> ChildContent { get; set; } }"
                    : "@ChildContent\n@code { [Parameter] public RenderFragment ChildContent { get; set; } }" : ""));
        }
        else
        {
            var body = wrapper is "default forwarding" or "mutated forwarding" ? "builder.AddContent(0, ChildContent);"
                : wrapper == "unknown" ? "builder.AddContent(0, System.Activator.CreateInstance<RenderFragment>());"
                : wrapper == "fixed invalid" ? "builder.OpenElement(0, \"span\"); builder.CloseElement();"
                : wrapper == "mixed" ? "builder.OpenComponent<Widget>(0); builder.CloseComponent();"
                : "builder.OpenComponent<Good>(0); builder.CloseComponent();";

            if (wrapper == "conditional frames")
            {
                body = """
                    if (Flag)
                    {
                        builder.OpenComponent<Good>(0);
                    }
                    else
                    {
                        builder.OpenElement(0, "span");
                    }

                    if (Flag)
                    {
                        builder.CloseComponent();
                    }
                    else
                    {
                        builder.CloseElement();
                    }
                    """;
            }

            var metadata = wrapper == "incompatible"
                ? "[assembly: PinguApps.BlazorContentContracts.CompiledContent(new System.Type[] { typeof(Good) }, typeof(Published.Wrapper), Version = 99)]\n" : "";
            File.WriteAllText(Path.Combine(directory, "Wrappers", "Wrapper.cs"), $$"""
                using Microsoft.AspNetCore.Components;
                {{metadata}}
                namespace Published;

                #pragma warning disable CS0612, CS0618
                {{(wrapper == "explicit" ? "[PinguApps.BlazorContentContracts.ProducesContent(typeof(Good))]" : "")}}
                public class Wrapper : ComponentBase
                {
                    {{(wrapper == "conditional frames" ? "[Parameter] public bool Flag { get; set; }" : "")}}
                    {{(wrapper == "default forwarding" ? "[Parameter] public RenderFragment ChildContent { get; set; } = builder => { builder.OpenElement(0, \"span\"); builder.CloseElement(); };" : "")}}
                    {{(wrapper == "mutated forwarding" ? "[Parameter] public RenderFragment ChildContent { get; set; }\nprotected override void OnInitialized()\n{\n    ChildContent = builder => { builder.OpenElement(0, \"span\"); builder.CloseElement(); };\n}" : "")}}

                    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
                    {
                        {{body}}
                    }
                }
                """);

            if (wrapper is "missing" or "incompatible")
            {
                var project = Path.Combine(directory, "Wrappers", "Wrappers.csproj");
                File.WriteAllText(project, File.ReadAllText(project).Replace("<PackageReference Include=\"PinguApps.BlazorContentContracts.Analyzers\" Version=\"1.0.0\" PrivateAssets=\"all\" />", ""));
            }
        }

        if (wrapper == "mixed")
        {
            File.WriteAllText(Path.Combine(directory, "Wrappers", "Widget.razor"), "@namespace Published\n@inherits Good\n");
        }

        if (wrapper is "obsolete" or "custom path")
        {
            var project = Path.Combine(directory, "Wrappers", "Wrappers.csproj");
            var properties = wrapper == "obsolete" ? "<TreatWarningsAsErrors>true</TreatWarningsAsErrors><NoWarn>ASP0006</NoWarn>"
                : "<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles><CompilerGeneratedFilesOutputPath>obj/custom-generated</CompilerGeneratedFilesOutputPath>";
            File.WriteAllText(project, File.ReadAllText(project).Replace("<PropertyGroup>", "<PropertyGroup>" + properties));
        }

        if (wrapper == "explicit")
        {
            File.WriteAllText(Path.Combine(directory, "Wrappers", "Factory.cs"), """
                using Microsoft.AspNetCore.Components;
                using PinguApps.BlazorContentContracts;

                namespace Published;

                public static class Factory
                {
                    [ProducesContent(typeof(Good))]
                    public static RenderFragment<int> Create() => context => builder => throw new System.InvalidOperationException("Application code must never execute during contract analysis");
                }
                """);
        }

        Run("Wrappers", "pack", "-o", directory);
        Assert.True(exitCode == 0, output);

        if (wrapper == "custom path")
        {
            Assert.True(Directory.EnumerateFiles(Path.Combine(directory, "Wrappers", "obj", "custom-generated"), "CompiledContent.g.cs", SearchOption.AllDirectories).Any());
        }

        Directory.Delete(Path.Combine(directory, "Menu"), recursive: true);

        if (wrapper != "project reference")
        {
            Directory.Delete(Path.Combine(directory, "Wrappers"), recursive: true);
        }

        Project("Consumer", "<PackageReference Include=\"Menu\" Version=\"1.0.0\" />"
            + (wrapper == "project reference" ? "<ProjectReference Include=\"../Wrappers/Wrappers.csproj\" />" : "<PackageReference Include=\"Wrappers\" Version=\"1.0.0\" />"));
        var usage = wrapper is "generic forwarding" or "typed forwarding" or "invalid forwarding"
            ? "<Published.Wrapper TItem=\"int\"><" + (wrapper == "invalid forwarding" ? "span" : "Good") + " /></Published.Wrapper>"
            : "<Published.Wrapper />";
        File.WriteAllText(Path.Combine(directory, "Consumer", "Usage.razor"), "@using Microsoft.AspNetCore.Components\n<Menu>" + usage + "</Menu>");

        if (wrapper.StartsWith("external", StringComparison.Ordinal))
        {
            var target = wrapper == "external precedence" ? "Published.Conflict"
                : wrapper == "external framework derived" ? "Microsoft.AspNetCore.Components.Forms.InputRadioGroup<>"
                : wrapper == "external generic" ? "Published.Wrapper<>" : "Published.Wrapper";
            var slotOwner = wrapper == "external generic" ? "Published.Wrapper<int>"
                : wrapper == "external framework derived" ? "Microsoft.AspNetCore.Components.Forms.InputRadioGroup<int>" : "Published.Wrapper";
            File.WriteAllText(Path.Combine(directory, "Consumer", "External.cs"), $$"""
                using PinguApps.BlazorContentContracts;
                [assembly: ExternalContent(new System.Type[0], typeof({{target}}){{(wrapper is "external default" or "external empty" ? ", HtmlElements = new[] { \"div\" }, Trusted = true" : "")}})]
                [assembly: ExternalForwarding(new[] { nameof({{slotOwner}}.ChildContent) }, typeof({{target}}))]
                """);
            usage = "<" + target + "><" + (wrapper == "external invalid forwarding" ? "span" : "Good") + " /></" + target + ">";

            if (wrapper == "external generic")
            {
                usage = "<Published.Wrapper TItem=\"int\"><span /></Published.Wrapper>";
            }

            if (wrapper is "external derived" or "external framework derived")
            {
                File.WriteAllText(Path.Combine(directory, "Consumer", "Derived.cs"), $$"""
                    public class Derived : {{(wrapper == "external framework derived" ? "Microsoft.AspNetCore.Components.Forms.InputRadioGroup<int>" : "Published.Wrapper")}}
                    {
                        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
                        {
                            builder.OpenElement(0, "span");
                            builder.CloseElement();
                        }
                    }
                    """);
                usage = "<Derived />";
            }

            File.WriteAllText(Path.Combine(directory, "Consumer", "Usage.razor"), "<Menu>" + usage + "</Menu>");
        }

        if (wrapper == "explicit")
        {
            File.AppendAllText(Path.Combine(directory, "Consumer", "Usage.razor"), "\n<Menu>@(Published.Factory.Create()(1))</Menu>");
        }
    }

    private void Project(string name, string references)
    {
        var path = Path.Combine(directory, name);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, name + ".csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>{{Environment.GetEnvironmentVariable("BCC_CONSUMER_FRAMEWORK") ?? "net11.0"}}</TargetFramework>
                <RestorePackagesPath>{{Path.Combine(directory, "packages")}}</RestorePackagesPath>
                <ErrorLog>diagnostics.sarif</ErrorLog>
              </PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                <PackageReference Include="PinguApps.BlazorContentContracts" Version="1.0.0" />
                <PackageReference Include="PinguApps.BlazorContentContracts.Analyzers" Version="1.0.0" PrivateAssets="all" />
                {{references}}
                <AdditionalFiles Include="**/*.razor" Exclude="bin/**;obj/**" />
              </ItemGroup>
            </Project>
            """);
    }

    public void Build()
    {
        Run("Consumer", "build");
    }

    public void Verify(string rule, int count)
    {
        using var sarif = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "Consumer", "diagnostics.sarif")));
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        Assert.DoesNotContain(results, result => result.GetProperty("ruleId").GetString() is "AD0001" or "CS8785");
        Assert.Equal(kind == "incompatible" ? 1 : 0, results.Count(result => result.GetProperty("ruleId").GetString() == "BCC014"));
        Assert.True(results.Count(result => result.GetProperty("ruleId").GetString() == rule) == count, output);

        Assert.Equal(rule is "BCC005" or "BCC009" ? 1 : 0, exitCode);
    }

    private void Run(string project, params string[] arguments)
    {
        var dotnet = Environment.GetEnvironmentVariable("BCC_CONSUMER_DOTNET") ?? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            ?? throw new InvalidOperationException("DOTNET_HOST_PATH must identify the SDK.");
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

        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("DOTNET_MSBUILD_SDK_RESOLVER_", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            start.Environment.Remove(name);
        }

        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        start.Environment["DOTNET_HOST_PATH"] = dotnet;
        start.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(dotnet)!;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(90000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Compiled library build exceeded 90 seconds.");
        }
        output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        exitCode = process.ExitCode;
    }

    public void Dispose()
    {
        if (Environment.GetEnvironmentVariable("BCC_KEEP_COMPILED_FIXTURES") != "1")
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
