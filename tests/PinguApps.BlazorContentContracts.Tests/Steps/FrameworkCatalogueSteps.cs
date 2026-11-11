using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PinguApps.BlazorContentContracts.Analyzers;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class FrameworkCatalogueSteps
{
    private CSharpCompilation compilation = null!;
    private INamedTypeSymbol component = null!;
    private FrameworkOutputDescription? description;
    private string? uncertainty;

    [Given("the selected framework component is constructed with int")]
    public void GivenConstructed()
    {
        component = component.Construct(compilation.GetSpecialType(SpecialType.System_Int32));
    }

    [Given("a constructed component derived from the selected framework component")]
    public void GivenConstructedDerived()
    {
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
            "public class Custom<T> : Microsoft.AspNetCore.Components.CascadingValue<T> { }"));
        component = compilation.GetTypeByMetadataName("Custom`1")!;
        GivenConstructed();
    }

    [Given("the installed framework component {string} from version {string}")]
    public void GivenFrameworkVersion(string name, string major)
    {
        var installed = Path.GetDirectoryName(typeof(Microsoft.AspNetCore.Components.ComponentBase).Assembly.Location)!;
        var framework = Directory.GetDirectories(Directory.GetParent(installed)!.FullName)
            .Where(path => Version.TryParse(Path.GetFileName(path), out var version) && version.Major.ToString() == major)
            .OrderByDescending(path => Version.Parse(Path.GetFileName(path))).First();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => Path.GetDirectoryName(path) == installed ? Path.Combine(framework, Path.GetFileName(path)) : path)
            .Where(File.Exists).Select(path => MetadataReference.CreateFromFile(path));
        compilation = CSharpCompilation.Create("Consumer", references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        component = compilation.GetTypeByMetadataName(name)!;
        Assert.NotNull(component);
        Assert.Equal(int.Parse(major), component.ContainingAssembly.Identity.Version.Major);
    }

    [Given("a framework identity with version {string} and authentic key {string}")]
    public void GivenVersion(string version, string authentic)
    {
        GivenComponent("Microsoft.AspNetCore.Components.CascadingValue`1");
        var key = component.ContainingAssembly.Identity.PublicKey;
        var library = CSharpCompilation.Create("Microsoft.AspNetCore.Components",
            new[] { CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyVersion(\"" + version
                + "\")] namespace Microsoft.AspNetCore.Components { public class CascadingValue<T> { public object ChildContent { get; set; } } }") },
            compilation.References.Where(reference => reference.Display == null
                || !Path.GetFileName(reference.Display).StartsWith("Microsoft.AspNetCore.Components", StringComparison.Ordinal)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                cryptoPublicKey: authentic == "yes" ? key : default, delaySign: authentic == "yes"));
        compilation = CSharpCompilation.Create("Consumer", references: new[] { library.ToMetadataReference() });
        component = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.CascadingValue`1")!;
    }

    [Given("the installed framework component {string}")]
    public void GivenComponent(string name)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        compilation = CSharpCompilation.Create("Consumer", references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        component = compilation.GetTypeByMetadataName(name)!;
        Assert.NotNull(component);
    }

    [Given("a user component named CascadingValue")]
    public void GivenImpostor()
    {
        GivenComponent("Microsoft.AspNetCore.Components.CascadingValue`1");
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace User { public class CascadingValue<T> { public object ChildContent { get; set; } } }"));
        component = compilation.GetTypeByMetadataName("User.CascadingValue`1")!;
    }

    [Given("a derived framework component")]
    public void GivenDerived()
    {
        GivenComponent("Microsoft.AspNetCore.Components.Web.ErrorBoundary");
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Custom : Microsoft.AspNetCore.Components.Web.ErrorBoundary { }"));
        component = compilation.GetTypeByMetadataName("Custom")!;
    }

    [When("the framework catalogue selects a description")]
    public void WhenSelect()
    {
        description = FrameworkBehaviourCatalogue.Find(component, compilation, out uncertainty);
    }

    [Then("the framework output kind is {string} with slots {string} and defaults {string}")]
    public void ThenDescription(string kind, string slots, string defaults)
    {
        Assert.Null(uncertainty);
        Assert.NotNull(description);
        Assert.Equal(kind, description.Kind);
        Assert.Equal(slots, string.Join(",", description.Slots));
        Assert.Equal(defaults, string.Join(",", description.DefaultElements));

        foreach (var slot in description.Slots)
        {
            var current = component;

            while (current != null && current.GetMembers(slot).IsEmpty)
            {
                current = current.BaseType;
            }

            Assert.NotNull(current);
        }
    }

    [Then("no framework description is granted")]
    public void ThenAbsent()
    {
        Assert.Null(description);
    }

    [Then("framework uncertainty explains {string}")]
    public void ThenUncertainty(string text)
    {
        Assert.Contains(text, uncertainty);
    }
}
