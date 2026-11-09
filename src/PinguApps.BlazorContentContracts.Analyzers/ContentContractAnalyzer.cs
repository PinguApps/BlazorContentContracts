using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace PinguApps.BlazorContentContracts.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ContentContractAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor InvalidChild = new(
        "BCC001", "Invalid child component", "Component '{0}' is not allowed in '{1}.{2}'",
        "ContentContracts", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(InvalidChild);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSemanticModelAction(Analyze);
    }

    private static void Analyze(SemanticModelAnalysisContext context)
    {
        var tree = context.SemanticModel.SyntaxTree;
        var root = tree.GetRoot(context.CancellationToken);
        var checksum = root.GetLeadingTrivia().Select(trivia => trivia.GetStructure())
            .OfType<PragmaChecksumDirectiveTriviaSyntax>().FirstOrDefault();

        if (checksum == null || !checksum.File.ValueText.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var file = context.Options.AdditionalFiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Path, checksum.File.ValueText, StringComparison.OrdinalIgnoreCase));
        var source = file?.GetText(context.CancellationToken);

        if (source == null)
        {
            return;
        }

        var opens = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(call => IsBuilderCall(call, "OpenComponent", context.SemanticModel, context.CancellationToken))
            .ToArray();
        var locations = MapTags(source, file!.Path, opens, context.SemanticModel, context.CancellationToken);

        foreach (var block in root.DescendantNodes().OfType<BlockSyntax>())
        {
            INamedTypeSymbol? owner = null;

            foreach (var statement in block.Statements.OfType<ExpressionStatementSyntax>())
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (statement.Expression is not InvocationExpressionSyntax call)
                {
                    continue;
                }

                if (IsBuilderCall(call, "OpenComponent", context.SemanticModel, context.CancellationToken))
                {
                    owner = ComponentType(call, context.SemanticModel, context.CancellationToken);
                }
                else if (IsBuilderCall(call, "CloseComponent", context.SemanticModel, context.CancellationToken))
                {
                    owner = null;
                }
                else if (owner != null && IsBuilderCall(call, "AddAttribute", context.SemanticModel, context.CancellationToken)
                    && call.ArgumentList.Arguments.Count == 3
                    && context.SemanticModel.GetConstantValue(call.ArgumentList.Arguments[1].Expression, context.CancellationToken).Value is string parameter)
                {
                    var property = owner.GetMembers(parameter).OfType<IPropertySymbol>().FirstOrDefault();
                    var contract = property?.GetAttributes().FirstOrDefault(attribute =>
                        attribute.AttributeClass?.ToDisplayString() == "PinguApps.BlazorContentContracts.AllowedChildrenAttribute");

                    if (contract == null || contract.ConstructorArguments.Length != 1)
                    {
                        continue;
                    }

                    var allowed = contract.ConstructorArguments[0].Values.Select(value => value.Value).OfType<ITypeSymbol>().ToArray();
                    var lambda = call.ArgumentList.Arguments[2].Expression.DescendantNodesAndSelf()
                        .OfType<LambdaExpressionSyntax>().FirstOrDefault();

                    if (lambda?.Body is not BlockSyntax content)
                    {
                        continue;
                    }

                    foreach (var child in content.Statements.OfType<ExpressionStatementSyntax>()
                        .Select(item => item.Expression).OfType<InvocationExpressionSyntax>()
                        .Where(item => IsBuilderCall(item, "OpenComponent", context.SemanticModel, context.CancellationToken)))
                    {
                        var type = ComponentType(child, context.SemanticModel, context.CancellationToken);

                        if (type != null && !allowed.Any(item => SymbolEqualityComparer.Default.Equals(item, type))
                            && locations.TryGetValue(child.SpanStart, out var location))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(InvalidChild, location, type.Name, owner.Name, parameter));
                        }
                    }
                }
            }
        }
    }

    private static bool IsBuilderCall(InvocationExpressionSyntax call, string name, SemanticModel model, System.Threading.CancellationToken cancellation)
    {
        return model.GetSymbolInfo(call, cancellation).Symbol is IMethodSymbol method
            && method.Name == name
            && method.ContainingType.ToDisplayString() == "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder";
    }

    private static INamedTypeSymbol? ComponentType(InvocationExpressionSyntax call, SemanticModel model, System.Threading.CancellationToken cancellation)
    {
        return (model.GetSymbolInfo(call, cancellation).Symbol as IMethodSymbol)?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
    }

    private static Dictionary<int, Location> MapTags(SourceText source, string path, InvocationExpressionSyntax[] opens,
        SemanticModel model, System.Threading.CancellationToken cancellation)
    {
        var result = new Dictionary<int, Location>();
        // This foundation slice deliberately accepts static markup only. Preserve offsets while hiding comments.
        var text = Regex.Replace(source.ToString(), @"@\*[\s\S]*?\*@|<!--[\s\S]*?-->",
            match => new string(' ', match.Length));

        if (text.Contains("@"))
        {
            return result;
        }

        var tags = Regex.Matches(text, @"<([A-Z][A-Za-z0-9_.]*)(?=[\s/>])")
            .Cast<Match>().ToArray();

        if (tags.Length != opens.Length)
        {
            return result;
        }

        for (var index = 0; index < opens.Length; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var type = ComponentType(opens[index], model, cancellation);
            var tag = tags[index].Groups[1];

            if (type == null || (tag.Value != type.Name && tag.Value != type.ToDisplayString()))
            {
                return new Dictionary<int, Location>();
            }

            var span = new TextSpan(tag.Index, tag.Length);
            result.Add(opens[index].SpanStart, Location.Create(path, span, source.Lines.GetLinePositionSpan(span)));
        }

        return result;
    }
}
