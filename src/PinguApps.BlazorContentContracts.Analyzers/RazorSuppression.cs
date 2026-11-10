using Microsoft.CodeAnalysis;

namespace PinguApps.BlazorContentContracts.Analyzers;

internal sealed class RazorSuppression
{
    internal RazorSuppression(Location location, int target, string? id)
    {
        Location = location;
        Target = target;
        Id = id;
    }

    internal Location Location { get; }
    internal int Target { get; }
    internal string? Id { get; }
    internal bool Used { get; set; }
}
