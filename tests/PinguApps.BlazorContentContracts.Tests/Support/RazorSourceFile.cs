using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal sealed class RazorSourceFile(string path, string content) : AdditionalText
{
    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        return SourceText.From(content, System.Text.Encoding.UTF8);
    }
}
