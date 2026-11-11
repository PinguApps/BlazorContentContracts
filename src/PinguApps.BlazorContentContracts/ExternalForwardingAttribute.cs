using System;

namespace PinguApps.BlazorContentContracts;

/// <summary>Slots emitted by the exact external component described by ExternalContent.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ExternalForwardingAttribute : Attribute
{
    public ExternalForwardingAttribute(string[] parameterNames, Type target)
    {
        ParameterNames = parameterNames;
        Target = target;
    }

    public string[] ParameterNames { get; }
    public Type Target { get; }
}
