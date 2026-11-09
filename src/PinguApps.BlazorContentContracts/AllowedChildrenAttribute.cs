using System;

namespace PinguApps.BlazorContentContracts;

/// <summary>Declares the component types permitted in a fragment parameter.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class AllowedChildrenAttribute : Attribute
{
    public AllowedChildrenAttribute(params Type[] componentTypes)
    {
        ComponentTypes = componentTypes;
    }

    public Type[] ComponentTypes { get; }

    /// <summary>Permitted HTML tag names. Empty by default; matching is case-insensitive.</summary>
    public string[] HtmlElements { get; set; } = Array.Empty<string>();

    /// <summary>Whether visible text is permitted. Empty content is always permitted.</summary>
    public bool AllowText { get; set; }
}
