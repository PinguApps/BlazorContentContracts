using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PinguApps.BlazorContentContracts.Analyzers;

internal sealed class FragmentInput
{
    public FragmentInput(ExpressionSyntax expression, SemanticModel model, Dictionary<string, FragmentInput> inputs)
    {
        Expression = expression;
        Model = model;
        Inputs = inputs;
    }

    public ExpressionSyntax Expression { get; }

    public SemanticModel Model { get; }

    public Dictionary<string, FragmentInput> Inputs { get; }
}
