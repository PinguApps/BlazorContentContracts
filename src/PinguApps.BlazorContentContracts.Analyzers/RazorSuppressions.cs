using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace PinguApps.BlazorContentContracts.Analyzers;

internal sealed class RazorSuppressions
{
    internal static readonly DiagnosticDescriptor Malformed = new(
        "BCC007", "Malformed Razor suppression", "Use 'bcc-suppress-next BCC004: reason' or 'bcc-suppress-next BCC006: reason' with a nonempty reason",
        "ContentContracts", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Unused = new(
        "BCC008", "Unused Razor suppression", "Suppression for '{0}' did not suppress the immediately following usage; remove or move the comment",
        "ContentContracts", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private readonly List<RazorSuppression> entries = new();

    internal RazorSuppressions(SourceText source, string path, IEnumerable<TextSpan> excludedSpans, System.Threading.CancellationToken cancellation)
    {
        var text = source.ToString();

        foreach (Match comment in Regex.Matches(text, @"@\*[\s\S]*?\*@|<!--[\s\S]*?-->"))
        {
            cancellation.ThrowIfCancellationRequested();

            if (!comment.Value.StartsWith("@*", StringComparison.Ordinal) || excludedSpans.Any(span => span.Contains(comment.Index)))
            {
                continue;
            }

            var body = comment.Value.Substring(2, comment.Length - 4).Trim();

            if (!body.StartsWith("bcc-suppress", StringComparison.Ordinal))
            {
                continue;
            }

            var match = Regex.Match(body, @"\Abcc-suppress-next\s+(BCC004|BCC006)\s*:\s*(\S[\s\S]*)\z");
            var target = comment.Index + comment.Length;

            while (target < text.Length && char.IsWhiteSpace(text[target]))
            {
                target++;
            }

            var span = new TextSpan(comment.Index, comment.Length);
            entries.Add(new RazorSuppression(Location.Create(path, span, source.Lines.GetLinePositionSpan(span)),
                target, match.Success ? match.Groups[1].Value : null));
        }
    }

    internal bool Suppresses(string id, Location location, SourceText source)
    {
        var start = location.SourceSpan.Start;

        foreach (var entry in entries)
        {
            // Component locations start after '<'; mapped Razor expressions start after '@'.
            if (entry.Id == id && (entry.Target == start
                || entry.Target + 1 == start && entry.Target < source.Length && source[entry.Target] is '<' or '@'
                || entry.Target + 2 == start && entry.Target + 1 < source.Length
                    && source[entry.Target] == '@' && source[entry.Target + 1] == '('))
            {
                entry.Used = true;
                return true;
            }
        }

        return false;
    }

    internal void Report(Action<Diagnostic> report)
    {
        foreach (var entry in entries)
        {
            if (entry.Id == null)
            {
                report(Diagnostic.Create(Malformed, entry.Location));
            }
            else if (!entry.Used)
            {
                report(Diagnostic.Create(Unused, entry.Location, entry.Id));
            }
        }
    }
}
