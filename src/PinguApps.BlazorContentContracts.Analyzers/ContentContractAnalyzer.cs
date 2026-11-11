using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    private static readonly DiagnosticDescriptor InvalidForwarding = new(
        "BCC009", "Incompatible content contract", "'{0}' declares output not allowed in '{1}.{2}'",
        "ContentContracts", DiagnosticSeverity.Error, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor RecursiveOutput = new(
        "BCC012", "Recursive content output", "Recursive output in '{0}.{1}': {2}; break the cycle or declare an output contract",
        "ContentContracts", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor AnalysisLimit = new(
        "BCC013", "Content analysis limit", "Analysis limit in '{0}.{1}': {2}; simplify the output or declare an output contract",
        "ContentContracts", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RecursiveOutput, AnalysisLimit, InvalidForwarding, InvalidChild, InvalidMarkup, UnknownMarkup, UnknownOutput, RazorSuppressions.Malformed, RazorSuppressions.Unused);

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
        // SDK 11 emits @code members in a separate partial declaration tree.
        var sourceRoots = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .SelectMany(declaration => context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)?.DeclaringSyntaxReferences
                ?? ImmutableArray<SyntaxReference>.Empty)
            .Select(reference => reference.SyntaxTree).Append(tree).Distinct()
            .Select(candidate => candidate.GetRoot(context.CancellationToken)).ToArray();
        var excludedSpans = sourceRoots.SelectMany(candidate => candidate.DescendantNodes())
            .Where(node => node is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax)
            .Select(literal => OriginalSpan(literal.GetLocation(), source, file.Path))
            .Concat(sourceRoots.SelectMany(candidate => candidate.DescendantTrivia())
                .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                .Select(trivia => OriginalSpan(trivia.GetLocation(), source, file.Path))).Where(span => span.HasValue)
            .Select(span => span!.Value).ToArray();
        var suppressions = new RazorSuppressions(source, file.Path, excludedSpans, context.CancellationToken);

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
                    var fallback = ownerLocation ?? Location.Create(file!.Path,
                        new TextSpan(0, 1), source.Lines.GetLinePositionSpan(new TextSpan(0, 1)));
                    var reportedUnknown = false;

                    var outputLocation = fallback;
                    var outputPath = "";

                    var html = contract.NamedArguments.FirstOrDefault(pair => pair.Key == "HtmlElements").Value;
                    var names = html.Kind == TypedConstantKind.Array && !html.IsNull
                        ? html.Values.Select(value => value.Value as string).ToArray() : Array.Empty<string>();
                    var allowText = contract.NamedArguments.Any(pair => pair.Key == "AllowText" && pair.Value.Value is true);

                    var reportedBounds = new HashSet<string>();
                    var traversal = new AnalysisTraversal(context.CancellationToken, ReportBound);
                    var renderBodies = new Dictionary<INamedTypeSymbol, (BlockSyntax? Body, bool Empty)>(SymbolEqualityComparer.Default);
                    var fragmentSources = new Dictionary<ExpressionSyntax, ExpressionSyntax?[]>();

                    if (CheckForwardedContract(value, context.SemanticModel))
                    {
                        continue;
                    }

                    ValidateValue(value, context.SemanticModel, new Dictionary<string, FragmentInput>(), "", null);

                    void ValidateValue(ExpressionSyntax expression, SemanticModel semantic, Dictionary<string, FragmentInput> inputs, string path, Location? usage)
                    {
                        if (!traversal.Enter(expression, path.Length == 0 ? "fragment" : path))
                        {
                            return;
                        }

                        try
                        {
                            context.CancellationToken.ThrowIfCancellationRequested();

                            if (!fragmentSources.TryGetValue(expression, out var candidates))
                            {
                                candidates = FragmentSources.Resolve(expression, semantic, context.CancellationToken, traversal: traversal.Fork()).ToArray();
                                fragmentSources.Add(expression, candidates);
                            }

                            foreach (var candidate in candidates)
                            {
                                if (candidate == null)
                                {
                                    ReportUnknown("fragment assignment, alias or method result is unestablished; inline the output or declare a source contract", expression, componentUsage: true);
                                    continue;
                                }

#pragma warning disable RS1030
                                var candidateModel = semantic.Compilation.GetSemanticModel(candidate.SyntaxTree);
#pragma warning restore RS1030
                                if (candidateModel.GetConstantValue(candidate, context.CancellationToken) is { HasValue: true, Value: null })
                                {
                                    continue;
                                }

                                if (CheckForwardedContract(candidate, candidateModel))
                                {
                                    continue;
                                }

                                var body = FragmentBody(candidate, candidateModel, context.CancellationToken,
                                    new HashSet<ISymbol>(SymbolEqualityComparer.Default));

                                if (body != null)
                                {
                                    Validate(body, candidateModel, inputs, path, usage);
                                }
                                else
                                {
                                    ReportUnknown("fragment body is unsupported; use an inspectable builder block", candidate);
                                }
                            }
                        }
                        finally
                        {
                            traversal.Exit();
                        }
                    }

                    void Validate(BlockSyntax body, SemanticModel model, Dictionary<string, FragmentInput> inputs, string path, Location? usage)
                    {
                        var componentDepth = 0;
                        var calls = ContentCalls(body, model, context.CancellationToken, new HashSet<SyntaxNode>(), traversal).ToArray();

                        foreach (var child in calls)
                        {
#pragma warning disable RS1030
                            model = model.Compilation.GetSemanticModel(child.SyntaxTree);
#pragma warning restore RS1030
                            outputLocation = usage ?? fallback;
                            outputPath = path;

                            if (IsBuilderCall(child, "CloseComponent", model, context.CancellationToken))
                            {
                                componentDepth--;
                                continue;
                            }

                            if (IsBuilderCall(child, "OpenComponent", model, context.CancellationToken))
                            {
                                var nested = componentDepth != 0;

                                if (model.GetSymbolInfo(child, context.CancellationToken).Symbol is IMethodSymbol opening
                                    && opening.ContainingType.ToDisplayString() == "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder")
                                {
                                    componentDepth++;
                                }

                                if (nested)
                                {
                                    continue;
                                }

                                var type = ComponentType(child, model, context.CancellationToken);

                                if (type != null && allowed.Any(item => Matches(item, type)))
                                {
                                    continue;
                                }

                                if (SymbolEqualityComparer.Default.Equals(type,
                                    model.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.DynamicComponent")))
                                {
                                    ReportUnknown("DynamicComponent selects its rendered component at runtime; use a statically selected component", child);
                                }
                                else if (type == null)
                                {
                                    ReportUnknown("the builder selects a component type dynamically; use OpenComponent<T>", child);
                                }
                                else
                                {
                                    var consumer = usage ?? (child.SyntaxTree == tree && locations.TryGetValue(child.SpanStart, out var mapped) ? mapped : fallback);
                                    var nextPath = path.Length == 0 ? type.Name : path + " -> " + type.Name;
                                    if (!renderBodies.TryGetValue(type, out var cachedRender))
                                    {
                                        var renderedBody = RenderBody(type, model.Compilation, context.CancellationToken, out var isEmpty);
                                        cachedRender = (renderedBody, isEmpty);
                                        renderBodies.Add(type, cachedRender);
                                    }

                                    var render = cachedRender.Body;
                                    var empty = cachedRender.Empty;

                                    if (empty)
                                    {
                                        continue;
                                    }

                                    var declaredOutput = OutputContractAnalyzer.OutputContract(type, model.Compilation);
                                    var framework = FrameworkBehaviourCatalogue.Find(type, model.Compilation, out var frameworkUncertainty);

                                    if (declaredOutput == null && render == null && framework?.Kind != "forwarding" && framework?.Kind != "alternatives")
                                    {
                                        outputLocation = consumer;
                                        outputPath = nextPath;
                                        ReportUnknown(frameworkUncertainty ?? "wrapper source is unavailable and compiled output metadata is missing, incompatible or unsupported; rebuild the library with summaries or declare its output", child);
                                        continue;
                                    }

                                    var arguments = new Dictionary<string, FragmentInput>();

                                    if (model.GetSymbolInfo(child, context.CancellationToken).Symbol is IMethodSymbol helper
                                        && InferredType(helper, model, context.CancellationToken) != null
                                        && helper.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) is MethodDeclarationSyntax declaration)
                                    {
                                        foreach (var attribute in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
                                        {
                                            if ((IsBuilderCall(attribute, "AddAttribute", model, context.CancellationToken)
                                                || IsBuilderCall(attribute, "AddComponentParameter", model, context.CancellationToken))
                                                && attribute.ArgumentList.Arguments.Count == 3
                                                && model.GetConstantValue(attribute.ArgumentList.Arguments[1].Expression, context.CancellationToken).Value is string inputName
                                                && model.GetSymbolInfo(attribute.ArgumentList.Arguments[2].Expression, context.CancellationToken).Symbol is IParameterSymbol helperParameter
                                                && helperParameter.Ordinal < child.ArgumentList.Arguments.Count)
                                            {
                                                arguments[inputName] = new FragmentInput(child.ArgumentList.Arguments[helperParameter.Ordinal].Expression, model, inputs);
                                            }
                                        }
                                    }

                                    var index = Array.IndexOf(calls, child) + 1;
                                    var depth = model.GetSymbolInfo(child, context.CancellationToken).Symbol is IMethodSymbol openingMethod
                                        && InferredType(openingMethod, model, context.CancellationToken) != null ? 0 : 1;

                                    for (; index < calls.Length && depth != 0; index++)
                                    {
                                        var attribute = calls[index];

                                        if (IsBuilderCall(attribute, "OpenComponent", model, context.CancellationToken))
                                        {
                                            depth++;
                                        }
                                        else if (IsBuilderCall(attribute, "CloseComponent", model, context.CancellationToken))
                                        {
                                            depth--;
                                        }
                                        else if (depth == 1 && (IsBuilderCall(attribute, "AddAttribute", model, context.CancellationToken)
                                            || IsBuilderCall(attribute, "AddComponentParameter", model, context.CancellationToken))
                                            && attribute.ArgumentList.Arguments.Count == 3
                                            && model.GetConstantValue(attribute.ArgumentList.Arguments[1].Expression, context.CancellationToken).Value is string inputName)
                                        {
                                            var argument = attribute.ArgumentList.Arguments[2].Expression;
                                            arguments[inputName] = new FragmentInput(argument, model, inputs);
                                        }
                                    }

                                    if (declaredOutput != null)
                                    {
                                        CheckDeclaredContract(declaredOutput, type);

                                        var forwarding = OutputContractAnalyzer.OutputContract(type, model.Compilation, "ForwardsContentAttribute");

                                        if (forwarding != null && forwarding.ConstructorArguments.Length >= 1)
                                        {
                                            foreach (var forwardedName in forwarding.ConstructorArguments[0].Values)
                                            {
                                                if (forwardedName.Value is string forwardedParameter && arguments.TryGetValue(forwardedParameter, out var input))
                                                {
                                                    if (!CheckForwardedContract(input.Expression, input.Model))
                                                    {
                                                        ValidateValue(input.Expression, input.Model, input.Inputs, nextPath + "." + forwardedParameter, consumer);
                                                    }
                                                }
                                            }
                                        }

                                        continue;
                                    }

                                    if (framework?.Kind is "forwarding" or "alternatives")
                                    {
                                        foreach (var slot in framework.Slots)
                                        {
                                            if (arguments.TryGetValue(slot, out var input)
                                                && !CheckForwardedContract(input.Expression, input.Model))
                                            {
                                                ValidateValue(input.Expression, input.Model, input.Inputs, nextPath + "." + slot, consumer);
                                            }
                                        }

                                        continue;
                                    }

                                    outputLocation = consumer;
                                    outputPath = nextPath;

                                    if (!traversal.Enter(type, nextPath))
                                    {
                                        continue;
                                    }

                                    // Wrapper implementations can live in another generated Razor tree.
                                    try
                                    {
#pragma warning disable RS1030
                                        Validate(render!, model.Compilation.GetSemanticModel(render!.SyntaxTree), arguments, nextPath, consumer);
#pragma warning restore RS1030
                                    }
                                    finally
                                    {
                                        traversal.Exit();
                                    }
                                }

                                continue;
                            }

                            if (componentDepth != 0)
                            {
                                continue;
                            }

                            var method = model.GetSymbolInfo(child, context.CancellationToken).Symbol as IMethodSymbol;

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
                            var constant = model.GetConstantValue(expression, context.CancellationToken);

                            if (IsBuilderCall(child, "OpenElement", model, context.CancellationToken)
                                && constant.Value is string name)
                            {
                                CheckElement(name);
                            }
                            else if (IsBuilderCall(child, "OpenElement", model, context.CancellationToken))
                            {
                                ReportUnknown("the builder selects an HTML element dynamically; use a constant element name", child);
                            }
                            else if (IsBuilderCall(child, "AddMarkupContent", model, context.CancellationToken)
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
                            else if (IsBuilderCall(child, "AddContent", model, context.CancellationToken)
                                || IsBuilderCall(child, "AddMarkupContent", model, context.CancellationToken))
                            {
                                if (CheckForwardedContract(expression, model))
                                {
                                    continue;
                                }

                                var forwarded = expression is InvocationExpressionSyntax invocation ? invocation.Expression : expression;
                                var forwardedSymbol = model.GetSymbolInfo(forwarded, context.CancellationToken).Symbol;

                                if (forwardedSymbol is IPropertySymbol property && IsFragment(property.Type))
                                {
                                    if (CheckForwardedContract(forwarded, model))
                                    {
                                        continue;
                                    }

                                    if (inputs.TryGetValue(property.Name, out var input))
                                    {
                                        var bound = input;
                                        var seen = new HashSet<FragmentInput>();

                                        while (seen.Add(bound))
                                        {
                                            var symbol = bound.Model.GetSymbolInfo(bound.Expression, context.CancellationToken).Symbol;

                                            if (symbol is IPropertySymbol forwardedProperty && bound.Inputs.TryGetValue(forwardedProperty.Name, out var previous))
                                            {
                                                bound = previous;
                                                continue;
                                            }

                                            break;
                                        }

                                        if (CheckForwardedContract(bound.Expression, bound.Model))
                                        {
                                            continue;
                                        }

                                        ValidateValue(bound.Expression, bound.Model, bound.Inputs, path + "." + property.Name, usage);
                                    }
                                    else if (path.Length == 0 || !property.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "Microsoft.AspNetCore.Components.ParameterAttribute")
                                        || property.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax(context.CancellationToken) is PropertyDeclarationSyntax declaration
                                            && (declaration.Initializer != null || declaration.ExpressionBody != null)))
                                    {
                                        ReportUnknown("the fragment value or its default implementation is opaque; annotate the source parameter with AllowedChildren or supply inline content", expression);
                                    }

                                    continue;
                                }

                                var type = model.GetTypeInfo(expression, context.CancellationToken).Type;

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

                    }

                    bool CheckForwardedContract(ExpressionSyntax expression, SemanticModel model)
                    {
                        while (expression is ParenthesizedExpressionSyntax or CastExpressionSyntax or InvocationExpressionSyntax)
                        {
                            expression = expression switch
                            {
                                ParenthesizedExpressionSyntax parentheses => parentheses.Expression,
                                CastExpressionSyntax cast => cast.Expression,
                                InvocationExpressionSyntax invocation => invocation.Expression,
                                _ => expression
                            };
                        }

                        var symbol = model.GetSymbolInfo(expression, context.CancellationToken).Symbol;
                        var forwarded = symbol is IPropertySymbol property && IsFragment(property.Type)
                            ? Contract(property.ContainingType, property.Name, model.Compilation)
                            : OutputContractAnalyzer.OutputContract(symbol, model.Compilation);

                        if (symbol == null)
                        {
                            return false;
                        }

                        if (forwarded == null || forwarded.ConstructorArguments.Length != 1)
                        {
                            return false;
                        }

                        CheckDeclaredContract(forwarded, symbol);

                        return true;
                    }

                    void CheckDeclaredContract(AttributeData forwarded, ISymbol symbol)
                    {
                        var types = forwarded.ConstructorArguments[0].Values.Select(item => item.Value).OfType<INamedTypeSymbol>();
                        var elements = forwarded.NamedArguments.FirstOrDefault(pair => pair.Key == "HtmlElements").Value;
                        var text = forwarded.NamedArguments.Any(pair => pair.Key == "AllowText" && pair.Value.Value is true);
                        var incompatible = types.Any(type => !allowed.Any(item => Matches(item, type)))
                            || text && !allowText
                            || elements.Kind == TypedConstantKind.Array && !elements.IsNull
                                && elements.Values.Any(element => !names.Any(name => string.Equals(name, element.Value as string, StringComparison.OrdinalIgnoreCase)));

                        if (incompatible)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(InvalidForwarding, outputLocation,
                                symbol.ToDisplayString(), owner.Name, parameter));
                        }
                    }

                    void ReportBound(bool recursive, string detail)
                    {
                        var rule = recursive ? RecursiveOutput : AnalysisLimit;

                        if (reportedBounds.Add(rule.Id))
                        {
                            var reason = recursive ? detail : "maximum depth " + AnalysisTraversal.MaximumDepth + " or expansion work " + AnalysisTraversal.MaximumWork + " exceeded (" + detail + ")";
                            context.ReportDiagnostic(Diagnostic.Create(rule, outputLocation, owner.Name, parameter, reason));
                        }
                    }

                    void ReportUnknown(string reason, SyntaxNode usage, bool componentUsage = false)
                    {
                        if (reportedUnknown)
                        {
                            return;
                        }

                        var location = outputPath.Length != 0 ? outputLocation : componentUsage ? ownerLocation : UsageLocation(usage);

                        if (location != null && suppressions.Suppresses("BCC006", location, source))
                        {
                            return;
                        }

                        context.ReportDiagnostic(Diagnostic.Create(UnknownOutput, location ?? fallback, owner.Name, parameter, reason + (outputPath.Length == 0 ? "" : " (via " + outputPath + ")")));
                        reportedUnknown = true;
                    }

                    Location? UsageLocation(SyntaxNode usage)
                    {
                        if (outputPath.Length != 0)
                        {
                            return outputLocation;
                        }

                        if (locations.TryGetValue(usage.SpanStart, out var componentLocation))
                        {
                            return componentLocation;
                        }

                        var emissionValue = usage.AncestorsAndSelf().OfType<ExpressionSyntax>().FirstOrDefault(candidate =>
                            candidate.Parent is ArgumentSyntax argument && argument.Parent?.Parent is InvocationExpressionSyntax emission
                            && emission.ArgumentList.Arguments.Count >= 2 && emission.ArgumentList.Arguments[1].Expression == candidate
                            && IsBuilderCall(emission, "AddContent", context.SemanticModel, context.CancellationToken));
                        var mapped = OriginalSpan((emissionValue ?? usage).GetLocation(), source, file.Path)
                            ?? OriginalSpan(usage.GetLocation(), source, file.Path);

                        if (mapped.HasValue)
                        {
                            return Location.Create(file.Path, mapped.Value, source.Lines.GetLinePositionSpan(mapped.Value));
                        }

                        return null;
                    }

                    string PathSuffix() => outputPath.Length == 0 ? "" : " (via " + outputPath + ")";

                    void CheckElement(string name)
                    {
                        if (!names.Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(InvalidMarkup, outputLocation, "HTML element '" + name + "'" + PathSuffix(), owner.Name, parameter));
                        }
                    }

                    void CheckText(string text)
                    {
                        if (!allowText && !string.IsNullOrWhiteSpace(text))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(InvalidMarkup, outputLocation, "Visible text" + PathSuffix(), owner.Name, parameter));
                        }
                    }
                }
            }
        }

        suppressions.Report(context.ReportDiagnostic);
    }

    internal static bool IsFragment(ITypeSymbol type)
    {
        return type is INamedTypeSymbol named && named.Name == "RenderFragment"
            && named.ContainingNamespace.ToDisplayString() == "Microsoft.AspNetCore.Components";
    }

    private static BlockSyntax? RenderBody(INamedTypeSymbol type, Compilation compilation,
        System.Threading.CancellationToken cancellation, out bool empty)
    {
        empty = false;

        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();

            if (SymbolEqualityComparer.Default.Equals(current, compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.ComponentBase")))
            {
                empty = true;
                return null;
            }

            if (current.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var method = current.GetMembers("BuildRenderTree").OfType<IMethodSymbol>()
                .FirstOrDefault(candidate => candidate.IsOverride && candidate.Parameters.Length == 1);

            if (method != null)
            {
                return (method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellation) as MethodDeclarationSyntax)?.Body;
            }
        }

        return null;
    }

    private static TextSpan? OriginalSpan(Location location, SourceText source, string path)
    {
        var mapped = location.GetMappedLineSpan();

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
        System.Threading.CancellationToken cancellation, HashSet<SyntaxNode> active, AnalysisTraversal traversal)
    {
        cancellation.ThrowIfCancellationRequested();

        if (!active.Add(node))
        {
            traversal.Cycle("fragment at " + node.GetLocation().GetLineSpan());
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
                    var expanded = false;
                    var uncertain = false;

                    foreach (var candidate in FragmentSources.Resolve(expression, model, cancellation, traversal: traversal))
                    {
                        if (candidate == null)
                        {
                            uncertain = true;
                            continue;
                        }

#pragma warning disable RS1030
                        var semantic = model.Compilation.GetSemanticModel(candidate.SyntaxTree);
#pragma warning restore RS1030
                        var body = FragmentBody(candidate, semantic, cancellation, new HashSet<ISymbol>(SymbolEqualityComparer.Default));

                        if (body == null)
                        {
                            uncertain = true;
                            continue;
                        }

                        expanded = true;

                        if (traversal.Enter(body, "fragment at " + body.GetLocation().GetLineSpan()))
                        {
                            try
                            {
                                foreach (var emitted in ContentCalls(body, semantic, cancellation, active, traversal))
                                {
                                    yield return emitted;
                                }
                            }
                            finally
                            {
                                traversal.Exit();
                            }
                        }
                    }

                    if (expanded && !uncertain)
                    {
                        continue;
                    }
                }

                yield return call;
                continue;
            }

            foreach (var emitted in ContentCalls(child, model, cancellation, active, traversal))
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

    internal static AttributeData? Contract(INamedTypeSymbol type, string parameter, Compilation compilation)
    {
        var attributeType = compilation.GetTypeByMetadataName("PinguApps.BlazorContentContracts.AllowedChildrenAttribute");

        AttributeData? inherited = null;

        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            var attribute = current.GetMembers(parameter).OfType<IPropertySymbol>()
                .SelectMany(property => property.GetAttributes())
                .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributeType));

            if (attribute != null)
            {
                inherited = attribute;
            }
        }

        return inherited;
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
