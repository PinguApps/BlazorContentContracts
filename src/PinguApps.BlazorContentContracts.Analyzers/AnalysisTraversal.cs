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
    private int work;

    internal AnalysisTraversal(CancellationToken cancellation, Action<bool, string>? report = null)
    {
        this.cancellation = cancellation;
        this.report = report;
    }

    internal bool Enter(object key, string label)
    {
        cancellation.ThrowIfCancellationRequested();

        if (active.Contains(key))
        {
            report?.Invoke(true, label);
            return false;
        }

        if (active.Count >= MaximumDepth || ++work > MaximumWork)
        {
            report?.Invoke(false, label);
            return false;
        }

        active.Add(key);
        return true;
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
