using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PinguApps.BlazorContentContracts.Analyzers;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests;

public sealed class AnalyzerSmokeTests
{
    [Fact]
    public async Task ValidCompilationRunsAnalyzerWithoutFailures()
    {
        var compilation = CSharpCompilation.Create("Consumer",
            new[] { CSharpSyntaxTree.ParseText("public class Component { }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics());
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ContentContractAnalyzer());
        Assert.Empty(await compilation.WithAnalyzers(analyzers).GetAllDiagnosticsAsync());
    }
}
