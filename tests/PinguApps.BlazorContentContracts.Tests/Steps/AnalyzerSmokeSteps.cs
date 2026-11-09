using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using PinguApps.BlazorContentContracts.Analyzers;
using PinguApps.BlazorContentContracts.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class AnalyzerSmokeSteps
{
    private Compilation compilation = null!;
    private ImmutableArray<Diagnostic> diagnostics;

    [Given("a valid consumer compilation")]
    public void GivenAValidConsumerCompilation()
    {
        compilation = ConsumerCompilation.Create();

        Assert.Empty(compilation.GetDiagnostics());
    }

    [When("the content contract analyzer runs")]
    public async Task WhenTheContentContractAnalyzerRuns()
    {
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ContentContractAnalyzer());

        diagnostics = await compilation.WithAnalyzers(analyzers).GetAllDiagnosticsAsync();
    }

    [Then("no diagnostics are reported")]
    public void ThenNoDiagnosticsAreReported()
    {
        Assert.Empty(diagnostics);
    }
}
