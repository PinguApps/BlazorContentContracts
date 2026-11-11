using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PinguApps.BlazorContentContracts.Analyzers;

internal static class FragmentSources
{
    // A null entry preserves uncertainty alongside any known output.
    internal static IEnumerable<ExpressionSyntax?> Resolve(ExpressionSyntax expression, SemanticModel model,
        CancellationToken cancellation, HashSet<ISymbol>? active = null)
    {
        cancellation.ThrowIfCancellationRequested();
        active ??= new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        switch (expression)
        {
            case ParenthesizedExpressionSyntax parentheses:
                return Resolve(parentheses.Expression, model, cancellation, active);
            case CastExpressionSyntax cast:
                return Resolve(cast.Expression, model, cancellation, active);
            case LambdaExpressionSyntax lambda:
                var delegateType = model.GetTypeInfo(lambda, cancellation).ConvertedType as INamedTypeSymbol;
                return delegateType?.Name == "RenderFragment"
                    && delegateType.ContainingNamespace.ToDisplayString() == "Microsoft.AspNetCore.Components"
                    ? new[] { expression } : new ExpressionSyntax?[] { null };
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                return Resolve(binary.Left, model, cancellation, active).Concat(Resolve(binary.Right, model, cancellation, active)).ToArray();
            case ConditionalExpressionSyntax conditional:
                return Resolve(conditional.WhenTrue, model, cancellation, active).Concat(Resolve(conditional.WhenFalse, model, cancellation, active)).ToArray();
        }

        if (model.GetConstantValue(expression, cancellation) is { HasValue: true, Value: null })
        {
            return Enumerable.Empty<ExpressionSyntax?>();
        }

        var symbol = model.GetSymbolInfo(expression, cancellation).Symbol;

        if ((OutputContractAnalyzer.OutputContract(symbol, model.Compilation) != null
            || symbol is IPropertySymbol propertyContract && propertyContract.GetAttributes()
                .Any(attribute => attribute.AttributeClass?.ToDisplayString() == "PinguApps.BlazorContentContracts.AllowedChildrenAttribute")))
        {
            return new[] { expression };
        }

        if (symbol is IMethodSymbol { MethodKind: MethodKind.DelegateInvoke } && expression is InvocationExpressionSyntax invocation)
        {
            return Resolve(invocation.Expression, model, cancellation, active);
        }

        if (symbol is not (ILocalSymbol or IFieldSymbol or IPropertySymbol or IMethodSymbol) || !active.Add(symbol) || active.Count > 64)
        {
            return new ExpressionSyntax?[] { null };
        }

        var values = new List<ExpressionSyntax?>();

        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax(cancellation);

            switch (declaration)
            {
                case VariableDeclaratorSyntax { Initializer: not null } variable:
                    Add(variable.Initializer.Value);
                    break;
                case PropertyDeclarationSyntax property:
                    if (property.Initializer != null)
                    {
                        Add(property.Initializer.Value);
                    }

                    if (property.ExpressionBody != null)
                    {
                        Add(property.ExpressionBody.Expression);
                    }

                    foreach (var getter in property.AccessorList?.Accessors.Where(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                        ?? Enumerable.Empty<AccessorDeclarationSyntax>())
                    {
                        if (getter.ExpressionBody != null)
                        {
                            Add(getter.ExpressionBody.Expression);
                        }

                        Returns(getter.Body);
                    }

                    break;
                case MethodDeclarationSyntax method:
                    if (method.ExpressionBody != null)
                    {
                        Add(method.ExpressionBody.Expression);
                    }

                    Returns(method.Body);
                    break;
            }
        }

        if (symbol is not IMethodSymbol)
        {
            // Whole-compilation may-write analysis deliberately ignores execution order.
            foreach (var tree in model.Compilation.SyntaxTrees)
            {
#pragma warning disable RS1030
                var semantic = model.Compilation.GetSemanticModel(tree);
#pragma warning restore RS1030

                foreach (var assignment in tree.GetRoot(cancellation).DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    cancellation.ThrowIfCancellationRequested();

                    if (!SymbolEqualityComparer.Default.Equals(semantic.GetSymbolInfo(assignment.Left, cancellation).Symbol, symbol))
                    {
                        continue;
                    }

                    if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) || assignment.IsKind(SyntaxKind.AddAssignmentExpression)
                        || assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression))
                    {
                        Add(assignment.Right);
                    }
                    else
                    {
                        values.Add(null);
                    }
                }

                foreach (var alias in tree.GetRoot(cancellation).DescendantNodes().OfType<RefExpressionSyntax>())
                {
                    if (SymbolEqualityComparer.Default.Equals(semantic.GetSymbolInfo(alias.Expression, cancellation).Symbol, symbol))
                    {
                        values.Add(null);
                    }
                }

                foreach (var argument in tree.GetRoot(cancellation).DescendantNodes().OfType<ArgumentSyntax>())
                {
                    if (argument.RefKindKeyword.RawKind != 0
                        && SymbolEqualityComparer.Default.Equals(semantic.GetSymbolInfo(argument.Expression, cancellation).Symbol, symbol))
                    {
                        values.Add(null);
                    }
                }
            }

            if ((symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.Protected
                || symbol is IPropertySymbol { SetMethod: not null } propertySymbol
                    && propertySymbol.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax(cancellation) is PropertyDeclarationSyntax property
                        && property.AccessorList?.Accessors.Any(accessor => accessor.Body != null || accessor.ExpressionBody != null) == true)))
            {
                values.Add(null);
            }
        }

        active.Remove(symbol);
        return values.Count == 0 ? new ExpressionSyntax?[] { null } : values;

        void Add(ExpressionSyntax value)
        {
#pragma warning disable RS1030
            var semantic = model.Compilation.GetSemanticModel(value.SyntaxTree);
#pragma warning restore RS1030
            values.AddRange(Resolve(value, semantic, cancellation, active));
        }

        void Returns(BlockSyntax? body)
        {
            if (body == null)
            {
                return;
            }

            foreach (var returned in body.DescendantNodes().OfType<ReturnStatementSyntax>()
                .Where(result => !result.Ancestors().TakeWhile(parent => parent != body)
                    .Any(parent => parent is LambdaExpressionSyntax or LocalFunctionStatementSyntax)))
            {
                if (returned.Expression != null)
                {
                    Add(returned.Expression);
                }
            }
        }
    }
}
