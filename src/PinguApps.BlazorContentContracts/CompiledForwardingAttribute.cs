using System;

namespace PinguApps.BlazorContentContracts;

/// <summary>Build-generated forwarded fragment parameters for a component.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class CompiledForwardingAttribute : Attribute
{
    public CompiledForwardingAttribute(string[] parameterNames, Type target)
    {
        ParameterNames = parameterNames;
        Target = target;
    }

    public string[] ParameterNames { get; }
    public Type Target { get; }
    public int Version { get; set; } = 1;
}
