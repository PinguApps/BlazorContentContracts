using System;

namespace PinguApps.BlazorContentContracts;

/// <summary>Describes fixed output from a component or a fragment-returning method.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class ProducesContentAttribute : Attribute
{
    public ProducesContentAttribute(params Type[] componentTypes)
    {
        ComponentTypes = componentTypes;
    }

    public Type[] ComponentTypes { get; }

    public string[] HtmlElements { get; set; } = Array.Empty<string>();

    public bool AllowText { get; set; }
}
