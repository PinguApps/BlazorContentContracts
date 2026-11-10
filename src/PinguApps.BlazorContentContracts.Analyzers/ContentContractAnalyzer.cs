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

    private static readonly DiagnosticDescriptor InvalidMarkup = new(
        "BCC005", "Invalid HTML or text", "{0} is not allowed in '{1}.{2}'",
        "ContentContracts", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnknownMarkup = new(
        "BCC004", "Unresolved markup output", "Markup output in '{0}.{1}' cannot be analysed; use inspectable content",
        "ContentContracts", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnknownOutput = new(
        "BCC006", "Unresolved content output", "Output in '{0}.{1}' cannot be analysed: {2}",
        "ContentContracts", DiagnosticSeverity.Warning, isEnabledByDefault: true,
        description: "Use inspectable fragments or supported builder calls to establish the emitted content. This is a compile-time check only.",
        helpLinkUri: "https://github.com/PinguApps/BlazorContentContracts#diagnostics");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(InvalidChild, InvalidMarkup, UnknownMarkup, UnknownOutput, RazorSuppressions.Malformed, RazorSuppressions.Unused);

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
        var literals = root.DescendantNodes().OfType<LiteralExpressionSyntax>()
            .Select(literal => OriginalSpan(literal, source, file.Path)).Where(span => span.HasValue)
            .Select(span => span!.Value).ToArray();
        var suppressions = new RazorSuppressions(source, file.Path, literals, context.CancellationToken);

        foreach (var block in root.DescendantNodes().OfType<BlockSyntax>())
        {
            if (InInferenceHelper(block))
            {
                continue;
            }

            INamedTypeSymbol? owner = null;
            Location? ownerLocation = null;

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
                    ownerLocation = locations.TryGetValue(call.SpanStart, out var mappedOwner) ? mappedOwner : null;
                }
                else if (IsBuilderCall(call, "CloseComponent", context.SemanticModel, context.CancellationToken))
                {
                    owner = null;
                }
                else if (owner != null && (IsBuilderCall(call, "AddAttribute", context.SemanticModel, context.CancellationToken)
                    || IsBuilderCall(call, "AddComponentParameter", context.SemanticModel, context.CancellationToken))
                    && call.ArgumentList.Arguments.Count == 3
                    && context.SemanticModel.GetConstantValue(call.ArgumentList.Arguments[1].Expression, context.CancellationToken).Value is string parameter)
                {
                    var contract = Contract(owner, parameter, context.SemanticModel.Compilation);

                    if (contract == null || contract.ConstructorArguments.Length != 1)
                    {
                        continue;
                    }

                    var allowed = contract.ConstructorArguments[0].Values.Select(value => value.Value).OfType<ITypeSymbol>().ToArray();
                    var value = call.ArgumentList.Arguments[2].Expression;
                    var content = FragmentBody(value, context.SemanticModel, context.CancellationToken,
                        new HashSet<ISymbol>(SymbolEqualityComparer.Default));
                    var fallback = ownerLocation ?? Location.Create(file!.Path,
                        new TextSpan(0, 1), source.Lines.GetLinePositionSpan(new TextSpan(0, 1)));
                    var reportedUnknown = false;

                    if (content == null)
                    {
                        ReportUnknown("the fragment value or method result is opaque; supply an inline fragment", value, componentUsage: true);
                        continue;
                    }

                    var html = contract.NamedArguments.FirstOrDefault(pair => pair.Key == "HtmlElements").Value;
                    var names = html.Kind == TypedConstantKind.Array && !html.IsNull
                        ? html.Values.Select(value => value.Value as string).ToArray() : Array.Empty<string>();
                    var allowText = contract.NamedArguments.Any(pair => pair.Key == "AllowText" && pair.Value.Value is true);
                    var componentDepth = 0;

                    foreach (var child in ContentCalls(content, context.SemanticModel, context.CancellationToken, new HashSet<SyntaxNode>()))
                    {
                        if (IsBuilderCall(child, "CloseComponent", context.SemanticModel, context.CancellationToken))
                        {
                            componentDepth--;
                            continue;
                        }

                        if (IsBuilderCall(child, "OpenComponent", context.SemanticModel, context.CancellationToken))
                        {
                            var nested = componentDepth != 0;

                            if (context.SemanticModel.GetSymbolInfo(child, context.CancellationToken).Symbol is IMethodSymbol opening
                                && opening.ContainingType.ToDisplayString() == "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder")
                            {
                                componentDepth++;
                            }

                            if (nested)
                            {
                                continue;
                            }

                            var type = ComponentType(child, context.SemanticModel, context.CancellationToken);

                            if (type != null && allowed.Any(item => Matches(item, type)))
                            {
                                continue;
                            }

                            if (SymbolEqualityComparer.Default.Equals(type,
                                context.SemanticModel.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.DynamicComponent")))
                            {
                                ReportUnknown("DynamicComponent selects its rendered component at runtime; use a statically selected component", child);
                            }
                            else if (type == null)
                            {
                                ReportUnknown("the builder selects a component type dynamically; use OpenComponent<T>", child);
                            }
                            else
                            {
                                var location = locations.TryGetValue(child.SpanStart, out var mapped) ? mapped : fallback;
                                context.ReportDiagnostic(Diagnostic.Create(InvalidChild, location, type.Name, owner.Name, parameter));
                            }

                            continue;
                        }

                        if (componentDepth != 0)
                        {
                            continue;
                        }

                        var method = context.SemanticModel.GetSymbolInfo(child, context.CancellationToken).Symbol as IMethodSymbol;

                        if (method?.ContainingType.ToDisplayString() != "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder")
                        {
                            if (child.Parent is ExpressionStatementSyntax)
                            {
                                ReportUnknown("a manual builder helper call has unsupported output; inline its builder operations", child);
                            }

                            continue;
                        }

                        if (child.ArgumentList.Arguments.Count < 2)
                        {
                            continue;
                        }

                        var expression = child.ArgumentList.Arguments[1].Expression;
                        var constant = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);

                        if (IsBuilderCall(child, "OpenElement", context.SemanticModel, context.CancellationToken)
                            && constant.Value is string name)
                        {
                            CheckElement(name);
                        }
                        else if (IsBuilderCall(child, "OpenElement", context.SemanticModel, context.CancellationToken))
                        {
                            ReportUnknown("the builder selects an HTML element dynamically; use a constant element name", child);
                        }
                        else if (IsBuilderCall(child, "AddMarkupContent", context.SemanticModel, context.CancellationToken)
                            && constant.Value is string markup)
                        {
                            // Only Razor's literal markup calls are parsed; MarkupString output stays opaque.
                            var remaining = Regex.Replace(markup, @"<!--[\s\S]*?-->|</?[A-Za-z](?:""[^""]*""|'[^']*'|[^'"">])*>", match =>
                            {
                                var tag = Regex.Match(match.Value, @"^<([A-Za-z][A-Za-z0-9._-]*)");

                                if (tag.Success)
                                {
                                    CheckElement(tag.Groups[1].Value);
                                }

                                return "";
                            });
                            CheckText(System.Net.WebUtility.HtmlDecode(remaining));
                        }
                        else if (IsBuilderCall(child, "AddContent", context.SemanticModel, context.CancellationToken)
                            || IsBuilderCall(child, "AddMarkupContent", context.SemanticModel, context.CancellationToken))
                        {
                            var type = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type;

                            if (type is INamedTypeSymbol fragment && fragment.Name == "RenderFragment"
                                && fragment.ContainingNamespace.ToDisplayString() == "Microsoft.AspNetCore.Components")
                            {
                                ReportUnknown("the fragment value or method result is opaque; supply an inline fragment", expression);
                            }
                            else if (type?.ToDisplayString() == "Microsoft.AspNetCore.Components.MarkupString"
                                || !constant.HasValue && type?.SpecialType != SpecialType.System_String)
                            {
                                var location = UsageLocation(expression);

                                if (location == null || !suppressions.Suppresses("BCC004", location, source))
                                {
                                    context.ReportDiagnostic(Diagnostic.Create(UnknownMarkup, location ?? fallback, owner.Name, parameter));
                                }
                            }
                            else
                            {
                                CheckText(constant.HasValue ? constant.Value?.ToString() ?? "" : "Text expression");
                            }
                        }
                    }

                    void ReportUnknown(string reason, SyntaxNode usage, bool componentUsage = false)
                    {
                        var location = componentUsage ? ownerLocation : UsageLocation(usage);

                        if (location != null && suppressions.Suppresses("BCC006", location, source))
                        {
                            return;
                        }

                        if (!reportedUnknown)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(UnknownOutput, location ?? fallback, owner.Name, parameter, reason));
                            reportedUnknown = true;
                        }
                    }

                    Location? UsageLocation(SyntaxNode usage)
                    {
                        if (locations.TryGetValue(usage.SpanStart, out var componentLocation))
                        {
                            return componentLocation;
                        }

                        var mapped = OriginalSpan(usage, source, file.Path);

                        if (mapped.HasValue)
                        {
                            return Location.Create(file.Path, mapped.Value, source.Lines.GetLinePositionSpan(mapped.Value));
                        }

                        return null;
                    }

                    void CheckElement(string name)
                    {
                        if (!names.Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(InvalidMarkup, fallback, "HTML element '" + name + "'", owner.Name, parameter));
                        }
                    }

                    void CheckText(string text)
                    {
                        if (!allowText && !string.IsNullOrWhiteSpace(text))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(InvalidMarkup, fallback, "Visible text", owner.Name, parameter));
                        }
                    }
                }
            }
        }

        suppressions.Report(context.ReportDiagnostic);
    }

    private static TextSpan? OriginalSpan(SyntaxNode node, SourceText source, string path)
    {
        var mapped = node.GetLocation().GetMappedLineSpan();

        if (!mapped.IsValid || !string.Equals(mapped.Path, path, StringComparison.OrdinalIgnoreCase)
            || mapped.StartLinePosition.Line >= source.Lines.Count || mapped.EndLinePosition.Line >= source.Lines.Count)
        {
            return null;
        }

        var startLine = source.Lines[mapped.StartLinePosition.Line];
        var endLine = source.Lines[mapped.EndLinePosition.Line];

        if (mapped.StartLinePosition.Character > startLine.Span.Length || mapped.EndLinePosition.Character > endLine.Span.Length)
        {
            return null;
        }

        return TextSpan.FromBounds(startLine.Start + mapped.StartLinePosition.Character, endLine.Start + mapped.EndLinePosition.Character);
    }

    private static IEnumerable<InvocationExpressionSyntax> ContentCalls(SyntaxNode node, SemanticModel model,
        System.Threading.CancellationToken cancellation, HashSet<SyntaxNode> active)
    {
        cancellation.ThrowIfCancellationRequested();

        if (!active.Add(node))
        {
            yield break;
        }

        foreach (var child in node.ChildNodes())
        {
            if (child is LambdaExpressionSyntax or LocalFunctionStatementSyntax)
            {
                continue;
            }

            if (child is InvocationExpressionSyntax call)
            {
                if (IsBuilderCall(call, "AddContent", model, cancellation) && call.ArgumentList.Arguments.Count >= 2)
                {
                    var expression = call.ArgumentList.Arguments[1].Expression;
                    var body = FragmentBody(expression, model, cancellation, new HashSet<ISymbol>(SymbolEqualityComparer.Default));

                    if (body != null)
                    {
                        foreach (var emitted in ContentCalls(body, model, cancellation, active))
                        {
                            yield return emitted;
                        }

                        continue;
                    }
                }

                yield return call;
                continue;
            }

            foreach (var emitted in ContentCalls(child, model, cancellation, active))
            {
                yield return emitted;
            }
        }

        active.Remove(node);
    }

    private static BlockSyntax? FragmentBody(ExpressionSyntax expression, SemanticModel model,
        System.Threading.CancellationToken cancellation, HashSet<ISymbol> active)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parentheses:
                return FragmentBody(parentheses.Expression, model, cancellation, active);
            case CastExpressionSyntax cast:
                return FragmentBody(cast.Expression, model, cancellation, active);
            case LambdaExpressionSyntax { Body: BlockSyntax body }:
                return body;
            case LambdaExpressionSyntax { Body: ExpressionSyntax result }:
                return FragmentBody(result, model, cancellation, active);
            case InvocationExpressionSyntax invocation:
                return FragmentBody(invocation.Expression, model, cancellation, active);
        }

        var symbol = model.GetSymbolInfo(expression, cancellation).Symbol;

        if (symbol is not ILocalSymbol local
            || local.Type is not INamedTypeSymbol fragmentType
            || fragmentType.ContainingNamespace.ToDisplayString() != "Microsoft.AspNetCore.Components"
            || fragmentType.Name != "RenderFragment"
            || !active.Add(symbol))
        {
            return null;
        }

        var declaration = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellation) as VariableDeclaratorSyntax;
        return declaration?.Initializer == null ? null : FragmentBody(declaration.Initializer.Value, model, cancellation, active);
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
