using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PinguApps.BlazorContentContracts.Analyzers;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class ExternalDescriptionSteps
{
    private CSharpCompilation compilation = null!;
    private ImmutableArray<Diagnostic> diagnostics;

    [Given("an external description '(.*)'")]
    public void GivenDescription(string description)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(ComponentBase).Assembly.Location).Append(typeof(AllowedChildrenAttribute).Assembly.Location)
            .Distinct().Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var library = CSharpCompilation.Create("ThirdParty", new[] { CSharpSyntaxTree.ParseText("""
            using Microsoft.AspNetCore.Components;
            public class Wrapper<T> : ComponentBase
            {
                [Parameter] public RenderFragment ChildContent { get; set; }
            }
            """) }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        Assert.True(library.Emit(stream).Success);
        var content = description switch
        {
            "trusted" => "[assembly: ExternalContent(new System.Type[0], typeof(Wrapper<int>), AllowText = true, Trusted = true)]",
            "missing trust" => "[assembly: ExternalContent(new System.Type[0], typeof(Wrapper<int>), AllowText = true)]",
            "invalid target" => "[assembly: ExternalContent(new System.Type[0], typeof(string))]",
            "null types" => "[assembly: ExternalContent(null, typeof(Wrapper<int>))]",
            "invalid html" => "[assembly: ExternalContent(new System.Type[0], typeof(Wrapper<int>), HtmlElements = new[] { \"<div>\" }, Trusted = true)]",
            "forwarding alone" => "",
            "duplicate slot" => "[assembly: ExternalContent(new System.Type[0], typeof(Wrapper<int>))][assembly: ExternalForwarding(new[] { \"ChildContent\", \"ChildContent\" }, typeof(Wrapper<int>))]",
            "generic" or "overlapping generic" => "[assembly: ExternalContent(new System.Type[0], typeof(Wrapper<>))]",
            _ => "[assembly: ExternalContent(new System.Type[0], typeof(Wrapper<int>))]"
        };

        if (description is "unknown slot" or "forwarding alone" or "transparent")
        {
            content += "[assembly: ExternalForwarding(new[] { \"" + (description == "unknown slot" ? "Typo" : "ChildContent") + "\" }, typeof(Wrapper<int>))]";
        }

        if (description is "duplicate" or "overlapping generic")
        {
            content += "[assembly: ExternalContent(new System.Type[0], typeof(Wrapper<int>))]";
        }

        compilation = CSharpCompilation.Create("Consumer", new[] { CSharpSyntaxTree.ParseText(
            "using PinguApps.BlazorContentContracts; " + content) }, references.Append(MetadataReference.CreateFromImage(stream.ToArray())),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.DoesNotContain(compilation.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [When("external description analysis runs")]
    public async Task WhenAnalysisRuns()
    {
        diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ExternalDescriptionAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    [Then("external description errors number (.*)")]
    public void ThenErrors(int count)
    {
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "AD0001");
        Assert.Equal(count, diagnostics.Count(diagnostic => diagnostic.Id == "BCC015"));
    }
}
