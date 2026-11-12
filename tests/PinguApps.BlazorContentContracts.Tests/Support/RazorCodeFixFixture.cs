using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using PinguApps.BlazorContentContracts.CodeFixes;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal static class RazorCodeFixFixture
{
    internal static async Task<string> ApplyAsync()
    {
        const string original = "<Slot>@Fragment @Fragment<Bad /></Slot>@code { private RenderFragment Fragment { get; set; } }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Consumer", LanguageNames.CSharp);
        var document = project.AddAdditionalDocument("Usage.razor", SourceText.From(original), filePath: "/Usage.razor");
        var source = await document.GetTextAsync();
        var span = new TextSpan(original.IndexOf("Fragment", StringComparison.Ordinal), "Fragment".Length);
        var descriptor = new DiagnosticDescriptor("BCC006", "Unknown output", "Unknown output", "ContentContracts",
            DiagnosticSeverity.Warning, true);
        var diagnostic = Diagnostic.Create(descriptor, Location.Create(document.FilePath!, span, source.Lines.GetLinePositionSpan(span)));
        var actions = new List<CodeAction>();
        var provider = new RazorSuppressionCodeFixProvider();
        var context = new CodeFixContext(document, span, ImmutableArray.Create(diagnostic),
            (action, _) => actions.Add(action), CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var operation = Assert.Single(operations.OfType<ApplyChangesOperation>());
        var updated = (await operation.ChangedSolution.GetAdditionalDocument(document.Id)!.GetTextAsync()).ToString();

        Assert.Equal(original, updated.Replace("@* bcc-suppress-next BCC006: reviewed output *@", "", StringComparison.Ordinal));
        Assert.Null(provider.GetFixAllProvider());
        Assert.DoesNotContain("BCC005", provider.FixableDiagnosticIds);
        return updated;
    }
}
