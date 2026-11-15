using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NET.Sdk.Razor.SourceGenerators;
using PinguApps.BlazorContentContracts.Analyzers;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal static class RazorCompiler
{
    private static readonly ImmutableArray<MetadataReference> References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Append(typeof(AllowedChildrenAttribute).Assembly.Location)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();

    internal static (int ExitCode, string Output) Analyse(string directory, bool analysis,
        string[]? referencePaths = null, bool emit = false, bool summaries = false)
    {
        VerificationMetrics.Compilation();
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(directory, path).Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parse, path, Encoding.UTF8))
            .Append(CSharpSyntaxTree.ParseText("", parse));
        var additional = Directory.GetFiles(directory, "*.razor", SearchOption.AllDirectories)
            .Select(path => (AdditionalText)new RazorSourceFile(path, File.ReadAllText(path))).ToImmutableArray();
        var projectPath = Directory.GetFiles(directory, "*.csproj").Single();
        var assemblyName = Path.GetFileNameWithoutExtension(projectPath);
        var project = File.ReadAllText(projectPath);
        var options = new RazorCompilerOptionsProvider(directory, project.Contains("<RootNamespace>Adoption</RootNamespace>", StringComparison.Ordinal)
            ? "Adoption" : assemblyName);
        var severity = ImmutableDictionary<string, ReportDiagnostic>.Empty;
        var config = Path.Combine(directory, ".globalconfig");

        if (File.Exists(config))
        {
            foreach (var line in File.ReadAllLines(config).Where(line => line.StartsWith("dotnet_diagnostic.", StringComparison.Ordinal)))
            {
                var parts = line.Split('=', 2);
                var id = parts[0].Trim().Split('.')[1];
                severity = severity.SetItem(id, parts[1].Trim() switch
                {
                    "error" => ReportDiagnostic.Error,
                    "none" => ReportDiagnostic.Suppress,
                    _ => ReportDiagnostic.Warn
                });
            }
        }

        var references = References.AddRange((referencePaths ?? Array.Empty<string>())
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)));
        var compilation = CSharpCompilation.Create(assemblyName, trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, specificDiagnosticOptions: severity,
                nullableContextOptions: project.Contains("<Nullable>enable</Nullable>", StringComparison.Ordinal)
                    ? NullableContextOptions.Enable : NullableContextOptions.Disable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new RazorSourceGenerator().AsSourceGenerator() },
            additional, parse, options);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var generatorDiagnostics);

        if (summaries && analysis)
        {
            GeneratorDriver summaryDriver = CSharpGeneratorDriver.Create(new[] { new CompiledContentGenerator().AsSourceGenerator() },
                additional, parse, options);
            summaryDriver.RunGeneratorsAndUpdateCompilation(generated, out var withSummaries, out var summaryDiagnostics);
            generated = withSummaries;
            generatorDiagnostics = generatorDiagnostics.AddRange(summaryDiagnostics);
        }

        var diagnostics = generated.GetDiagnostics().AddRange(generatorDiagnostics);

        if (analysis)
        {
            var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ContentContractAnalyzer(), new DeclarationAnalyzer(),
                new OutputContractAnalyzer(), new ExternalDescriptionAnalyzer(), new CompiledMetadataAnalyzer());
            diagnostics = generated.WithAnalyzers(analyzers, new AnalyzerOptions(additional, options))
                .GetAllDiagnosticsAsync().GetAwaiter().GetResult().AddRange(generatorDiagnostics);
        }

        if (emit && !diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            using var image = File.Create(Path.Combine(directory, assemblyName + ".dll"));
            diagnostics = diagnostics.AddRange(generated.Emit(image).Diagnostics);
        }

        diagnostics = diagnostics.Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Distinct().ToImmutableArray();

        var results = diagnostics.Select(diagnostic =>
        {
            var span = diagnostic.Location.GetMappedLineSpan();
            return new
            {
                ruleId = diagnostic.Id,
                level = diagnostic.Severity == DiagnosticSeverity.Error ? "error" : "warning",
                message = diagnostic.GetMessage(),
                locations = new[] { new { resultFile = new { uri = span.Path,
                    region = new { startLine = span.StartLinePosition.Line + 1, startColumn = span.StartLinePosition.Character + 1,
                        endLine = span.EndLinePosition.Line + 1, endColumn = span.EndLinePosition.Character + 1 } } } }
            };
        });
        File.WriteAllText(Path.Combine(directory, "diagnostics.sarif"), JsonSerializer.Serialize(new { runs = new[] { new { results } } }));
        return (diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ? 1 : 0,
            string.Join(Environment.NewLine, diagnostics));
    }
}
