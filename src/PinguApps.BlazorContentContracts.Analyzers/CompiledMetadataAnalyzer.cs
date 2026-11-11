using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PinguApps.BlazorContentContracts.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CompiledMetadataAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Incompatible = new(
        "BCC014", "Incompatible compiled content metadata", "Compiled content metadata cannot be trusted: {0}",
        "ContentContracts", DiagnosticSeverity.Warning, isEnabledByDefault: true, customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Incompatible);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(compilation =>
        {
            var annotations = compilation.Compilation.GetTypeByMetadataName("PinguApps.BlazorContentContracts.ProducesContentAttribute");

            if ((annotations != null || compilation.Compilation.SourceModule.ReferencedAssemblySymbols.Any(assembly =>
                assembly.GetTypeByMetadataName("PinguApps.BlazorContentContracts.ProducesContentAttribute") != null))
                && !Compatible(compilation.Compilation))
            {
                compilation.ReportDiagnostic(Diagnostic.Create(Incompatible, Location.None, "annotation assembly identity or schema version is unsupported"));
            }

            foreach (var assembly in compilation.Compilation.SourceModule.ReferencedAssemblySymbols)
            {
                compilation.CancellationToken.ThrowIfCancellationRequested();

                foreach (var attribute in assembly.GetAttributes().Where(attribute => attribute.AttributeClass?.ToDisplayString()
                    is "PinguApps.BlazorContentContracts.CompiledContentAttribute" or "PinguApps.BlazorContentContracts.CompiledForwardingAttribute"))
                {
                    if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass,
                            compilation.Compilation.GetTypeByMetadataName(attribute.AttributeClass!.ToDisplayString()))
                        || attribute.ConstructorArguments.Length != 2 || attribute.ConstructorArguments[1].Value is not INamedTypeSymbol target
                        || !ValidSummary(attribute, target))
                    {
                        compilation.ReportDiagnostic(Diagnostic.Create(Incompatible, Location.None, assembly.Name + " publishes a malformed or unsupported summary; rebuild it with compatible annotations"));
                    }
                }
            }
        });
    }

    internal static bool Compatible(Compilation compilation)
    {
        var type = compilation.GetTypeByMetadataName("PinguApps.BlazorContentContracts.ProducesContentAttribute");

        if (type?.ContainingAssembly.Identity.Name != "PinguApps.BlazorContentContracts")
        {
            return false;
        }

        if (new[] { "AllowedChildrenAttribute", "ForwardsContentAttribute" }.Any(name =>
            !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly,
                compilation.GetTypeByMetadataName("PinguApps.BlazorContentContracts." + name)?.ContainingAssembly)))
        {
            return false;
        }

        var versions = type.ContainingAssembly.GetAttributes().Where(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "System.Reflection.AssemblyMetadataAttribute"
                && attribute.ConstructorArguments.Length == 2
                && Equals(attribute.ConstructorArguments[0].Value, "BlazorContentContracts.SchemaVersion")).ToArray();
        return versions.Length == 1 && Equals(versions[0].ConstructorArguments[1].Value, "1");
    }

    internal static bool ValidSummary(AttributeData attribute, INamedTypeSymbol target)
    {
        if (attribute.ConstructorArguments.Length != 2 || attribute.ConstructorArguments[0].IsNull
            || attribute.ConstructorArguments[0].Kind != TypedConstantKind.Array
            || attribute.NamedArguments.Any(pair => pair.Key == "Version" && !Equals(pair.Value.Value, 1)))
        {
            return false;
        }

        var forwarding = attribute.AttributeClass?.Name == "CompiledForwardingAttribute";
        return attribute.ConstructorArguments[0].Values.All(value => forwarding
            ? value.Value is string name && target.OriginalDefinition.GetMembers(name).OfType<IPropertySymbol>().Any(property => ContentContractAnalyzer.IsFragment(property.Type))
            : value.Value is INamedTypeSymbol component && component.OriginalDefinition.AllInterfaces.Any(item => item.ToDisplayString() == "Microsoft.AspNetCore.Components.IComponent"))
            && !attribute.NamedArguments.Any(pair => pair.Key == "HtmlElements" && (pair.Value.IsNull
                || pair.Value.Values.Any(value => value.Value is not string)));
    }
}
