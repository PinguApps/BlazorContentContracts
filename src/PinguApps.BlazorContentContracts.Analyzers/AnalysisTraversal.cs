using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace PinguApps.BlazorContentContracts.Analyzers;

internal sealed class AnalysisTraversal
{
    internal const int MaximumDepth = 64;
    internal const int MaximumWork = 4096;
    private readonly List<object> active = new();
    private readonly CancellationToken cancellation;
    private readonly Action<bool, string>? report;
    private readonly AnalysisTraversal? parent;
    private int work;

    internal AnalysisTraversal(CancellationToken cancellation, Action<bool, string>? report = null, AnalysisTraversal? parent = null)
    {
        this.cancellation = cancellation;
        this.report = report;
        this.parent = parent;
    }

    internal bool Enter(object key, string label)
    {
        cancellation.ThrowIfCancellationRequested();

        if (active.Exists(item => item is ISymbol left && key is ISymbol right
            ? SymbolEqualityComparer.Default.Equals(left, right) : ReferenceEquals(item, key)))
        {
            report?.Invoke(true, label);
            return false;
        }

        var depth = active.Count;
        var budget = this;

        while (budget.parent != null)
        {
            budget = budget.parent;
            depth += budget.active.Count;
        }

        if (depth >= MaximumDepth || ++budget.work > MaximumWork)
        {
            report?.Invoke(false, label);
            return false;
        }

        active.Add(key);
        return true;
    }

    internal AnalysisTraversal Fork()
    {
        return new AnalysisTraversal(cancellation, report, this);
    }

    internal void Cycle(string label)
    {
        cancellation.ThrowIfCancellationRequested();
        report?.Invoke(true, label);
    }

    internal void Exit()
    {
        active.RemoveAt(active.Count - 1);
    }
}
