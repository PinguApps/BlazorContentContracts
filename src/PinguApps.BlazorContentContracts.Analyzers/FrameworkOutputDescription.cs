using System.Collections.Immutable;

namespace PinguApps.BlazorContentContracts.Analyzers;

// The same union of fixed output and forwarded slots as external descriptions,
// with explicit locality and defaults for framework-specific renderers.
internal sealed class FrameworkOutputDescription
{
    internal FrameworkOutputDescription(string kind, string[] slots, string[] defaultElements)
    {
        Kind = kind;
        Slots = ImmutableArray.CreateRange(slots);
        DefaultElements = ImmutableArray.CreateRange(defaultElements);
    }

    internal string Kind { get; }
    internal ImmutableArray<string> Slots { get; }
    internal ImmutableArray<string> DefaultElements { get; }
}
