using System;

namespace PinguApps.BlazorContentContracts;

/// <summary>Build-generated fixed output for a component in this assembly.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class CompiledContentAttribute : Attribute
{
    public CompiledContentAttribute(Type[] componentTypes, Type target)
    {
        ComponentTypes = componentTypes;
        Target = target;
    }

    public Type[] ComponentTypes { get; }
    public Type Target { get; }
    public int Version { get; set; } = 1;
    public string[] HtmlElements { get; set; } = Array.Empty<string>();
    public bool AllowText { get; set; }
}
