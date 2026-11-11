using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PinguApps.BlazorContentContracts.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OutputContractAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Invalid = new(
        "BCC010", "Output contradicts declaration", "Output of '{0}' contradicts its ProducesContent contract: {1}",
        "ContentContracts", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor Unknown = new(
        "BCC011", "Unresolved declared output", "Output of '{0}' cannot be verified: {1}",
        "ContentContracts", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Invalid, Unknown);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.Method);
    }

    internal static AttributeData? OutputContract(ISymbol? symbol, Compilation compilation, string attributeName = "ProducesContentAttribute")
    {
        var attributeType = compilation.GetTypeByMetadataName("PinguApps.BlazorContentContracts." + attributeName);
        AttributeData? result = null;

        while (symbol != null)
        {
            var attribute = symbol.GetAttributes().FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributeType));

            if (attribute != null)
            {
                result = attribute;
            }

            symbol = symbol switch
            {
                IMethodSymbol method => method.OverriddenMethod,
                INamedTypeSymbol type => type.BaseType,
                _ => null
            };
        }

        return result;
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        var contract = OutputContract(method, context.Compilation);
        var component = method.Name == "BuildRenderTree" && method.IsOverride;

        if (component)
        {
            contract = OutputContract(method.ContainingType, context.Compilation);
        }

        if (contract == null || contract.ConstructorArguments.Length != 1)
        {
            return;
        }

        var declaration = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) as MethodDeclarationSyntax;

        if (declaration == null)
        {
            return;
        }

        var allowed = contract.ConstructorArguments[0].Values.Select(value => value.Value).OfType<ITypeSymbol>().ToArray();
        var html = contract.NamedArguments.FirstOrDefault(pair => pair.Key == "HtmlElements").Value;
        var names = html.Kind == TypedConstantKind.Array && !html.IsNull
            ? html.Values.Select(value => value.Value as string).ToArray() : Array.Empty<string>();
        var text = contract.NamedArguments.Any(pair => pair.Key == "AllowText" && pair.Value.Value is true);
        var active = new HashSet<SyntaxNode>();
        var unknown = false;
#pragma warning disable RS1030
        var model = context.Compilation.GetSemanticModel(declaration.SyntaxTree);
#pragma warning restore RS1030

        if (component)
        {
            Inspect(declaration.Body ?? (SyntaxNode?)declaration.ExpressionBody, model, true);
        }
        else if (declaration.ExpressionBody != null)
        {
            Inspect(declaration.ExpressionBody.Expression, model, false);
        }
        else if (declaration.Body != null)
        {
            foreach (var result in declaration.Body.DescendantNodes().OfType<ReturnStatementSyntax>()
                .Where(result => !result.Ancestors().TakeWhile(node => node != declaration.Body)
                    .Any(node => node is LambdaExpressionSyntax or LocalFunctionStatementSyntax)))
            {
                Inspect(result.Expression, model, false);
            }
        }

        void Report(bool invalid, string reason)
        {
            if (!invalid && unknown)
            {
                return;
            }

            unknown |= !invalid;
            context.ReportDiagnostic(Diagnostic.Create(invalid ? Invalid : Unknown,
                component ? contract.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                    ?? declaration.Identifier.GetLocation() : declaration.Identifier.GetLocation(), method.ToDisplayString(), reason));
        }

        void Inspect(SyntaxNode? node, SemanticModel semantic, bool builder)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (node == null || !active.Add(node))
            {
                Report(false, "implementation is unavailable or recursive");
                return;
            }

            if (!builder)
            {
                switch (node)
                {
                    case ParenthesizedExpressionSyntax parentheses:
                        Inspect(parentheses.Expression, semantic, false);
                        break;
                    case CastExpressionSyntax cast:
                        Inspect(cast.Expression, semantic, false);
                        break;
                    case LambdaExpressionSyntax lambda:
                        Inspect(lambda.Body, semantic, lambda.Body is BlockSyntax
                            || lambda.Body is InvocationExpressionSyntax invocation
                                && semantic.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol emitted
                                && emitted.ContainingType.ToDisplayString() == "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder");
                        break;
                    case ConditionalExpressionSyntax conditional:
                        Inspect(conditional.WhenTrue, semantic, false);
                        Inspect(conditional.WhenFalse, semantic, false);
                        break;
                    case SwitchExpressionSyntax selection:
                        foreach (var arm in selection.Arms)
                        {
                            Inspect(arm.Expression, semantic, false);
                        }

                        break;
                    default:
                        var symbol = semantic.GetSymbolInfo(node, context.CancellationToken).Symbol;
                        var declared = OutputContract(symbol, context.Compilation);

                        if (declared != null)
                        {
                            CheckPermissions(declared);
                            break;
                        }

                        if (symbol is IMethodSymbol factory && factory.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken)
                            is MethodDeclarationSyntax factoryDeclaration)
                        {
#pragma warning disable RS1030
                            var factoryModel = context.Compilation.GetSemanticModel(factoryDeclaration.SyntaxTree);
#pragma warning restore RS1030

                            if (factoryDeclaration.ExpressionBody != null)
                            {
                                Inspect(factoryDeclaration.ExpressionBody.Expression, factoryModel, false);
                            }
                            else if (factoryDeclaration.Body != null)
                            {
                                foreach (var returned in factoryDeclaration.Body.DescendantNodes().OfType<ReturnStatementSyntax>()
                                    .Where(result => !result.Ancestors().TakeWhile(parent => parent != factoryDeclaration.Body)
                                        .Any(parent => parent is LambdaExpressionSyntax or LocalFunctionStatementSyntax)))
                                {
                                    Inspect(returned.Expression, factoryModel, false);
                                }
                            }
                            else
                            {
                                Report(false, "factory implementation is unavailable");
                            }

                            break;
                        }

                        var forwarding = OutputContract(method.ContainingType, context.Compilation, "ForwardsContentAttribute");

                        if (component && symbol is IPropertySymbol property && forwarding != null
                            && forwarding.ConstructorArguments.Length == 1
                            && forwarding.ConstructorArguments[0].Values.Any(value => value.Value as string == property.Name))
                        {
                            break;
                        }

                        if (node is ExpressionSyntax expression && semantic.GetConstantValue(expression, context.CancellationToken) is { HasValue: true, Value: null })
                        {
                            break;
                        }

                        Report(false, "fragment result is opaque");
                        break;
                }
            }
            else
            {
                var depth = 0;

                foreach (var call in node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                    .Where(call => !call.Ancestors().TakeWhile(parent => parent != node).Any(parent => parent is LambdaExpressionSyntax or LocalFunctionStatementSyntax)))
                {
                    var invoked = semantic.GetSymbolInfo(call, context.CancellationToken).Symbol as IMethodSymbol;

                    if (invoked?.ContainingType.ToDisplayString() != "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder")
                    {
                        if (call.Parent is ExpressionStatementSyntax)
                        {
                            Report(false, "builder helper output is opaque");
                        }

                        continue;
                    }

                    if (invoked.Name == "CloseComponent")
                    {
                        depth--;
                        continue;
                    }

                    if (invoked.Name == "OpenComponent")
                    {
                        if (depth++ == 0)
                        {
                            var type = invoked.TypeArguments.FirstOrDefault() as INamedTypeSymbol;

                            if (type == null)
                            {
                                Report(false, "dynamic component type");
                            }
                            else if (!allowed.Any(item => Matches(item, type)))
                            {
                                Report(true, "component " + type.Name);
                            }
                        }

                        continue;
                    }

                    if (depth != 0 || call.ArgumentList.Arguments.Count < 2)
                    {
                        continue;
                    }

                    var value = call.ArgumentList.Arguments[1].Expression;
                    var constant = semantic.GetConstantValue(value, context.CancellationToken);

                    if (invoked.Name == "OpenElement")
                    {
                        if (constant.Value is string name)
                        {
                            if (!names.Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)))
                            {
                                Report(true, "HTML element " + name);
                            }
                        }
                        else
                        {
                            Report(false, "dynamic HTML element");
                        }
                    }
                    else if (invoked.Name == "AddContent")
                    {
                        if (constant.HasValue)
                        {
                            if (!text && !string.IsNullOrWhiteSpace(constant.Value?.ToString()))
                            {
                                Report(true, "visible text");
                            }
                        }
                        else if (semantic.GetTypeInfo(value, context.CancellationToken).Type?.SpecialType == SpecialType.System_String)
                        {
                            if (!text)
                            {
                                Report(true, "visible text");
                            }
                        }
                        else
                        {
                            Inspect(value, semantic, false);
                        }
                    }
                    else if (invoked.Name == "AddMarkupContent")
                    {
                        if (constant.Value is string markup)
                        {
                            var remaining = Regex.Replace(markup, @"<!--[\s\S]*?-->|</?[A-Za-z](?:""[^""]*""|'[^']*'|[^'"">])*>", match =>
                            {
                                var tag = Regex.Match(match.Value, @"^<([A-Za-z][A-Za-z0-9._-]*)");

                                if (tag.Success && !names.Any(item => string.Equals(item, tag.Groups[1].Value, StringComparison.OrdinalIgnoreCase)))
                                {
                                    Report(true, "HTML element " + tag.Groups[1].Value);
                                }

                                return "";
                            });

                            if (!text && !string.IsNullOrWhiteSpace(System.Net.WebUtility.HtmlDecode(remaining)))
                            {
                                Report(true, "visible text");
                            }
                        }
                        else
                        {
                            Report(false, "markup output is opaque");
                        }
                    }
                }
            }

            active.Remove(node);
        }

        void CheckPermissions(AttributeData produced)
        {
            var components = produced.ConstructorArguments[0].Values.Select(value => value.Value).OfType<INamedTypeSymbol>();
            var elements = produced.NamedArguments.FirstOrDefault(pair => pair.Key == "HtmlElements").Value;

            if (components.Any(type => !allowed.Any(item => Matches(item, type)))
                || !text && produced.NamedArguments.Any(pair => pair.Key == "AllowText" && pair.Value.Value is true)
                || elements.Kind == TypedConstantKind.Array && !elements.IsNull
                    && elements.Values.Any(value => !names.Any(name => string.Equals(name, value.Value as string, StringComparison.OrdinalIgnoreCase))))
            {
                Report(true, "called declaration permits incompatible output");
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
}
