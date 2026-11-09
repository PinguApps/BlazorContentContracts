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
}
