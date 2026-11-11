using System;

namespace PinguApps.BlazorContentContracts;

/// <summary>A consumer-owned description of a referenced component's fixed and alternative output.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ExternalContentAttribute : Attribute
{
    public ExternalContentAttribute(Type[] componentTypes, Type target)
    {
        ComponentTypes = componentTypes;
        Target = target;
    }

    public Type[] ComponentTypes { get; }
    public Type Target { get; }
    public string[] HtmlElements { get; set; } = Array.Empty<string>();
    public bool AllowText { get; set; }
    public bool Trusted { get; set; }
}
