using System;

namespace PinguApps.BlazorContentContracts;

/// <summary>Names component fragment parameters whose output is forwarded transparently.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class ForwardsContentAttribute : Attribute
{
    public ForwardsContentAttribute(params string[] parameterNames)
    {
        ParameterNames = parameterNames;
    }

    public string[] ParameterNames { get; }
}
