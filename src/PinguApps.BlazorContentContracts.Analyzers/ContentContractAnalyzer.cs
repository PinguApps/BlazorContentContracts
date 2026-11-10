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
            .Where(call => !InInferenceHelper(call) && IsBuilderCall(call, "OpenComponent", context.SemanticModel, context.CancellationToken))
            .ToArray();
        var locations = MapTags(source, file!.Path, opens, context.SemanticModel, context.CancellationToken);

        foreach (var block in root.DescendantNodes().OfType<BlockSyntax>())
        {
            if (InInferenceHelper(block))
            {
                continue;
            }

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
                    var contract = Contract(owner, parameter, context.SemanticModel.Compilation);

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

                        if (type != null && !allowed.Any(item => Matches(item, type))
                            && locations.TryGetValue(child.SpanStart, out var location))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(InvalidChild, location, type.Name, owner.Name, parameter));
                        }
                    }
                }
            }
        }
    }

    private static bool Matches(ITypeSymbol allowed, INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(allowed, current)
                || allowed is INamedTypeSymbol { IsUnboundGenericType: true } open
                && SymbolEqualityComparer.Default.Equals(open.OriginalDefinition, current.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static AttributeData? Contract(INamedTypeSymbol type, string parameter, Compilation compilation)
    {
        var attributeType = compilation.GetTypeByMetadataName("PinguApps.BlazorContentContracts.AllowedChildrenAttribute");

        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            var attribute = current.GetMembers(parameter).OfType<IPropertySymbol>()
                .SelectMany(property => property.GetAttributes())
                .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributeType));

            if (attribute != null)
            {
                return attribute;
            }
        }

        return null;
    }

    private static bool InInferenceHelper(SyntaxNode node)
    {
        return node.Ancestors().OfType<ClassDeclarationSyntax>().Any(type => type.Identifier.ValueText == "TypeInference")
            && node.Ancestors().OfType<NamespaceDeclarationSyntax>()
                .Any(space => space.Name.ToString().StartsWith("__Blazor.", StringComparison.Ordinal));
    }

    private static bool IsBuilderCall(InvocationExpressionSyntax call, string name, SemanticModel model, System.Threading.CancellationToken cancellation)
    {
        return model.GetSymbolInfo(call, cancellation).Symbol is IMethodSymbol method
            && (method.Name == name
                && method.ContainingType.ToDisplayString() == "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder"
                || name == "OpenComponent" && InferredType(method, model, cancellation) != null);
    }

    private static INamedTypeSymbol? ComponentType(InvocationExpressionSyntax call, SemanticModel model, System.Threading.CancellationToken cancellation)
    {
        var method = model.GetSymbolInfo(call, cancellation).Symbol as IMethodSymbol;

        if (method == null)
        {
            return null;
        }

        return InferredType(method, model, cancellation) ?? method.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
    }

    private static INamedTypeSymbol? InferredType(IMethodSymbol method, SemanticModel model, System.Threading.CancellationToken cancellation)
    {
        if (method.ContainingType.Name != "TypeInference" || !method.ContainingNamespace.ToDisplayString().StartsWith("__Blazor.", StringComparison.Ordinal))
        {
            return null;
        }

        var declaration = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellation);

        if (declaration == null || declaration.SyntaxTree != model.SyntaxTree)
        {
            return null;
        }

        foreach (var call in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(call, cancellation).Symbol is IMethodSymbol open
                && open.Name == "OpenComponent"
                && open.ContainingType.ToDisplayString() == "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder"
                && open.TypeArguments.FirstOrDefault() is INamedTypeSymbol type)
            {
                return type.ConstructedFrom.Construct(type.TypeArguments.Select(argument =>
                    argument is ITypeParameterSymbol parameter && parameter.TypeParameterKind == TypeParameterKind.Method
                        ? method.TypeArguments[parameter.Ordinal] : argument).ToArray());
            }
        }

        return null;
    }

    private static Dictionary<int, Location> MapTags(SourceText source, string path, InvocationExpressionSyntax[] opens,
        SemanticModel model, System.Threading.CancellationToken cancellation)
    {
        var result = new Dictionary<int, Location>();
        // This foundation slice deliberately accepts static markup only. Preserve offsets while hiding comments.
        var text = Regex.Replace(source.ToString(), @"@\*[\s\S]*?\*@|<!--[\s\S]*?-->",
            match => new string(' ', match.Length));

        text = Regex.Replace(text, @"(?m)^\s*@using[^\r\n]*", match => new string(' ', match.Length));

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

            if (type == null || (tag.Value != type.Name && tag.Value != type.ContainingNamespace + "." + type.Name))
            {
                return new Dictionary<int, Location>();
            }

            var span = new TextSpan(tag.Index, tag.Length);
            result.Add(opens[index].SpanStart, Location.Create(path, span, source.Lines.GetLinePositionSpan(span)));
        }

        return result;
    }
}
