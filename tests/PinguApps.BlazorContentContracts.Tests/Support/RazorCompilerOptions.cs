using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal sealed class RazorCompilerOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
{
    public override bool TryGetValue(string key, out string value)
    {
        return values.TryGetValue(key, out value!);
    }
}
