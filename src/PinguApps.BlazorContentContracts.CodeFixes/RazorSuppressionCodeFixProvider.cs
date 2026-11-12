using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace PinguApps.BlazorContentContracts.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RazorSuppressionCodeFixProvider),
    DocumentKinds = new[] { nameof(TextDocumentKind.Document), nameof(TextDocumentKind.AdditionalDocument) }), Shared]
public sealed class RazorSuppressionCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("BCC004", "BCC006");

    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.TextDocument;


        foreach (var diagnostic in context.Diagnostics)
        {
            var target = document;
            var start = diagnostic.Location.SourceSpan.Start;

            if (diagnostic.Properties.TryGetValue("bcc_path", out var path)
                && diagnostic.Properties.TryGetValue("bcc_start", out var positionText)
                && int.TryParse(positionText, out var originalStart))
            {
                target = document.Project.AdditionalDocuments.FirstOrDefault(candidate =>
                    string.Equals(candidate.FilePath, path, StringComparison.OrdinalIgnoreCase));
                start = originalStart;
            }

            if (target == null || target.FilePath == null || !target.FilePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var source = await target.GetTextAsync(context.CancellationToken).ConfigureAwait(false);

            if (start > source.Length)
            {
                continue;
            }

            if (start > 1 && source[start - 2] == '@' && source[start - 1] == '(')
            {
                start -= 2;
            }
            else if (start > 0 && source[start - 1] is '<' or '@')
            {
                start--;
            }

            if (start >= source.Length || source[start] is not ('<' or '@'))
            {
                continue;
            }

            var id = diagnostic.Id;
            var position = start;
            context.RegisterCodeFix(CodeAction.Create("Acknowledge reviewed output with a Razor comment",
                cancellation =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    var comment = "@* bcc-suppress-next " + id + ": reviewed output *@";
                    var updated = source.WithChanges(new TextChange(new TextSpan(position, 0), comment));
                    return Task.FromResult(target.Project.Solution.WithAdditionalDocumentText(target.Id, updated));
                }, nameof(RazorSuppressionCodeFixProvider) + "." + id), diagnostic);
        }
    }
}
