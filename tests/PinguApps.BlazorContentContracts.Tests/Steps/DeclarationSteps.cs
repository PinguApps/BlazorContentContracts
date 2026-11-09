using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
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
public sealed class DeclarationSteps
{
    private CSharpCompilation compilation = null!;
    private ImmutableArray<Diagnostic> diagnostics;
    private INamedTypeSymbol metadata = null!;

    [Given("a contract declaration '(.*)'")]
    public void GivenDeclaration(string declaration)
    {
        var body = declaration switch
        {
            "components" => "[Parameter, AllowedChildren(typeof(Fixture))] public RenderFragment ChildContent { get; set; }",
            "html" => "[Parameter, AllowedChildren(HtmlElements = new[] { \"div\", \"my-element\", \"SVG\" }, AllowText = true)] public RenderFragment ChildContent { get; set; }",
            "typed" => "[Parameter, AllowedChildren] public RenderFragment<int> ChildContent { get; set; }",
            "method" => "[ProducesContent(typeof(Fixture))] public RenderFragment Content() => throw new Exception();",
            "typed method" => "[ProducesContent] public RenderFragment<int> Content() => throw new Exception();",
            "invalid type" => "[Parameter, AllowedChildren(typeof(string))] public RenderFragment ChildContent { get; set; }",
            "invalid generic" => "[Parameter, AllowedChildren(typeof(System.Collections.Generic.List<>))] public RenderFragment ChildContent { get; set; }",
            "invalid element" => "[Parameter, AllowedChildren(HtmlElements = new[] { \"<div>\" })] public RenderFragment ChildContent { get; set; }",
            "null types" => "[Parameter, AllowedChildren(null)] public RenderFragment ChildContent { get; set; }",
            "null elements" => "[Parameter, AllowedChildren(HtmlElements = null)] public RenderFragment ChildContent { get; set; }",
            "wrong property" => "[Parameter, AllowedChildren] public string ChildContent { get; set; }",
            "missing parameter" => "[AllowedChildren] public RenderFragment ChildContent { get; set; }",
            "static parameter" => "[Parameter, AllowedChildren] public static RenderFragment ChildContent { get; set; }",
            "forwarding" => "[Parameter, AllowedChildren(typeof(Fixture), HtmlElements = new[] { \"span\" }, AllowText = true)] public RenderFragment ChildContent { get; set; }",
            "wrong method" => "[ProducesContent] public string Content() => throw new Exception();",
            "hidden forward" => "public new string ChildContent { get; set; }",
            "identical override" => "[Parameter, AllowedChildren(typeof(Fixture))] public override RenderFragment ChildContent { get; set; }",
            "conflicting override" => "[Parameter, AllowedChildren(AllowText = true)] public override RenderFragment ChildContent { get; set; }",
            _ => "[Parameter] public RenderFragment ChildContent { get; set; }"
        };
        var attributes = declaration switch
        {
            "output" => "[ProducesContent(HtmlElements = new[] { \"div\" })]",
            "wrong component" => "[ProducesContent]",
            "forwarding" or "inherited forwarding" or "hidden forward" => "[ProducesContent, ForwardsContent(\"ChildContent\")]",
            "missing forward" => "[ForwardsContent(\"Missing\")]",
            "duplicate forward" => "[ForwardsContent(\"ChildContent\", \"ChildContent\")]",
            "conflicting output" => "[ProducesContent(AllowText = true)]",
            "conflicting forwarding" => "[ForwardsContent(\"Other\")]",
            _ => ""
        };
        var inherits = declaration.Contains("override", StringComparison.Ordinal)
            || declaration is "inherited forwarding" or "hidden forward" or "conflicting output" or "conflicting forwarding";
        var baseName = declaration == "wrong component" ? "object" : inherits ? "BaseFixture" : "ComponentBase";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(ComponentBase).Assembly.Location).Append(typeof(AllowedChildrenAttribute).Assembly.Location)
            .Distinct().Select(path => MetadataReference.CreateFromFile(path));
        const string imports = "using System; using Microsoft.AspNetCore.Components; using PinguApps.BlazorContentContracts; ";
        var sources = new[]
        {
            CSharpSyntaxTree.ParseText(imports + attributes + " public class Fixture : " + baseName + " { " + body + " }", path: "Fixture.cs"),
            CSharpSyntaxTree.ParseText(imports + "[ProducesContent, ForwardsContent(\"ChildContent\")] public class BaseFixture : ComponentBase { [Parameter, AllowedChildren(typeof(Fixture))] public virtual RenderFragment ChildContent { get; set; } [Parameter] public RenderFragment Other { get; set; } }", path: "BaseFixture.cs")
        };
        compilation = CSharpCompilation.Create("DeclarationConsumer", sources, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Assert.DoesNotContain(compilation.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [When("declaration analysis runs")]
    public async Task WhenAnalysisRuns()
    {
        diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new DeclarationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    [Then("the declaration diagnostic is '(.*)'")]
    public void ThenDiagnostic(string expected)
    {
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "AD0001");

        if (expected == "none")
        {
            Assert.Empty(diagnostics);
        }
        else
        {
            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == expected
                && diagnostic.Location.GetLineSpan().Path == "Fixture.cs"
                && diagnostic.GetMessage().Length > 30);
        }
    }

    [When("the declaration is emitted and read as metadata")]
    public void WhenEmitted()
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));

        var consumer = compilation.RemoveAllSyntaxTrees().AddReferences(MetadataReference.CreateFromImage(stream.ToArray()));
        metadata = consumer.GetTypeByMetadataName("Fixture")!;
    }

    [Then("schema v1 identities and defaults are preserved")]
    public void ThenMetadata()
    {
        Assert.Contains(metadata.GetAttributes(), attribute => attribute.AttributeClass!.ToDisplayString()
            == "PinguApps.BlazorContentContracts.ProducesContentAttribute");
        var forwarding = Assert.Single(metadata.GetAttributes().Where(attribute => attribute.AttributeClass!.Name == "ForwardsContentAttribute"));
        Assert.Equal("ChildContent", forwarding.ConstructorArguments[0].Values[0].Value);
        var input = Assert.Single(metadata.GetMembers("ChildContent").Single().GetAttributes()
            .Where(attribute => attribute.AttributeClass!.Name == "AllowedChildrenAttribute"));
        Assert.Equal("Fixture", ((ITypeSymbol)input.ConstructorArguments[0].Values[0].Value!).Name);
        Assert.Equal("span", input.NamedArguments.Single(argument => argument.Key == "HtmlElements").Value.Values[0].Value);
        Assert.Equal(true, input.NamedArguments.Single(argument => argument.Key == "AllowText").Value.Value);
        Assert.Empty(new AllowedChildrenAttribute().ComponentTypes);
        Assert.Empty(new AllowedChildrenAttribute().HtmlElements);
        Assert.False(new AllowedChildrenAttribute().AllowText);
        Assert.Empty(new ProducesContentAttribute().ComponentTypes);
        Assert.Empty(new ProducesContentAttribute().HtmlElements);
        Assert.False(new ProducesContentAttribute().AllowText);
        Assert.Empty(new ForwardsContentAttribute().ParameterNames);

        foreach (var type in new[] { typeof(AllowedChildrenAttribute), typeof(ProducesContentAttribute), typeof(ForwardsContentAttribute) })
        {
            Assert.Equal("PinguApps.BlazorContentContracts." + type.Name, type.FullName);
            Assert.True(type.IsSealed);
            Assert.Equal(typeof(Attribute), type.BaseType);
            var usage = type.GetCustomAttribute<AttributeUsageAttribute>()!;
            Assert.True(usage.Inherited);
            Assert.False(usage.AllowMultiple);
            Assert.Equal(type == typeof(AllowedChildrenAttribute) ? AttributeTargets.Property
                : type == typeof(ProducesContentAttribute) ? AttributeTargets.Class | AttributeTargets.Method
                : AttributeTargets.Class, usage.ValidOn);
        }

        Assert.Contains(typeof(AllowedChildrenAttribute).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>(),
            attribute => attribute.Key == "BlazorContentContracts.SchemaVersion" && attribute.Value == "1");
    }
}
