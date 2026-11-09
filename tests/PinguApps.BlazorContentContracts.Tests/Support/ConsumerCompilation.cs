using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal static class ConsumerCompilation
{
    public static CSharpCompilation Create()
    {
        return CSharpCompilation.Create("Consumer",
            new[] { CSharpSyntaxTree.ParseText("public class Component { }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
