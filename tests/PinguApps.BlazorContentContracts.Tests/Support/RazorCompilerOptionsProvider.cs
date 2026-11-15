using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal sealed class RazorCompilerOptionsProvider(string directory, string rootNamespace) : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions { get; } = new RazorCompilerOptions(new Dictionary<string, string>
    {
        ["build_property.RazorConfiguration"] = "MVC-3.0",
        ["build_property.RazorLangVersion"] = "Latest",
        ["build_property.RootNamespace"] = rootNamespace,
        ["build_property.MSBuildProjectDirectory"] = directory
    });

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return GlobalOptions;
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        return new RazorCompilerOptions(new Dictionary<string, string>
        {
            ["build_metadata.AdditionalFiles.TargetPath"] = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(Path.GetRelativePath(directory, textFile.Path)))
        });
    }
}
