using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PinguApps.BlazorContentContracts.Analyzers;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal static class MetadataConflictFixture
{
    public static async Task Verify(string conflict)
    {
        var framework = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).StartsWith("PinguApps.BlazorContentContracts", StringComparison.Ordinal))
            .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var canonical = Assembly("PinguApps.BlazorContentContracts", conflict == "unsupported version" ? "2" : "1", framework);
        var references = framework.Cast<MetadataReference>().Append(canonical);

        if (conflict == "conflicting identity")
        {
            references = references.Append(Assembly("ConflictingAnnotations", "1", framework));
        }

        var consumer = CSharpCompilation.Create("Consumer", new[] { CSharpSyntaxTree.ParseText("public class Consumer\n{\n}\n") }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = await consumer.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CompiledMetadataAnalyzer())).GetAnalyzerDiagnosticsAsync();

        Assert.Equal("BCC014", Assert.Single(diagnostics).Id);
    }

    private static MetadataReference Assembly(string name, string version, MetadataReference[] references)
    {
        var declarations = new[] { "ProducesContentAttribute", "AllowedChildrenAttribute", "ForwardsContentAttribute" }
            .Select(type => CSharpSyntaxTree.ParseText("namespace PinguApps.BlazorContentContracts;\npublic sealed class " + type + " : System.Attribute\n{\n}\n", path: type + ".cs"));
        var marker = CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyMetadata(\"BlazorContentContracts.SchemaVersion\", \"" + version + "\")]", path: "AssemblyInfo.cs");
        var compilation = CSharpCompilation.Create(name, declarations.Append(marker), references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(image.ToArray());
    }
}
