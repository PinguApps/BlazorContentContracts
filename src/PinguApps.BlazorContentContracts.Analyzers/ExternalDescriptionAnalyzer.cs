using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PinguApps.BlazorContentContracts.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExternalDescriptionAnalyzer : DiagnosticAnalyzer
{
    private const string Prefix = "PinguApps.BlazorContentContracts.";
    private static readonly DiagnosticDescriptor Invalid = new(
        "BCC015", "Invalid external content description", "Invalid external description: {0}",
        "ContentContracts", DiagnosticSeverity.Error, isEnabledByDefault: true, customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Invalid);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(action =>
        {
            foreach (var attribute in Descriptions(action.Compilation))
            {
                action.CancellationToken.ThrowIfCancellationRequested();
                var error = Validate(attribute, action.Compilation);

                if (error != null)
                {
                    action.ReportDiagnostic(Diagnostic.Create(Invalid,
                        attribute.ApplicationSyntaxReference?.GetSyntax(action.CancellationToken).GetLocation() ?? Location.None, error));
                }
            }
        });
    }

    internal static AttributeData? Find(INamedTypeSymbol component, Compilation compilation, bool forwarding)
    {
        var matches = Descriptions(compilation).Where(attribute => Target(attribute) is INamedTypeSymbol target
            && Matches(target, component)).ToArray();
        var content = matches.Where(attribute => attribute.AttributeClass?.Name == "ExternalContentAttribute").ToArray();

        if (content.Length != 1 || matches.Any(attribute => Validate(attribute, compilation) != null))
        {
            return null;
        }

        return forwarding ? matches.SingleOrDefault(attribute => attribute.AttributeClass?.Name == "ExternalForwardingAttribute") : content[0];
    }

    internal static bool HasMapping(INamedTypeSymbol component, Compilation compilation)
    {
        return Descriptions(compilation).Any(attribute => Target(attribute) is INamedTypeSymbol target && Matches(target, component));
    }

    private static ImmutableArray<AttributeData> Descriptions(Compilation compilation)
    {
        return compilation.Assembly.GetAttributes().Where(attribute => new[] { "ExternalContentAttribute", "ExternalForwardingAttribute" }
            .Any(name => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, compilation.GetTypeByMetadataName(Prefix + name))))
            .ToImmutableArray();
    }

    private static INamedTypeSymbol? Target(AttributeData attribute)
    {
        return attribute.ConstructorArguments.Length == 2 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
    }

    private static bool Matches(INamedTypeSymbol target, INamedTypeSymbol component)
    {
        return SymbolEqualityComparer.Default.Equals(target, component)
            || target.IsUnboundGenericType && SymbolEqualityComparer.Default.Equals(target.OriginalDefinition, component.OriginalDefinition);
    }

    private static string? Validate(AttributeData attribute, Compilation compilation)
    {
        var target = Target(attribute);

        if (target == null || target.TypeKind != TypeKind.Class
            || SymbolEqualityComparer.Default.Equals(target.ContainingAssembly, compilation.Assembly)
            || !target.OriginalDefinition.AllInterfaces.Any(type => SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.IComponent"))))
        {
            return "target must be a resolved IComponent class in a referenced assembly";
        }

        var peers = Descriptions(compilation).Where(candidate => Target(candidate) is INamedTypeSymbol other
            && (Matches(target, other) || Matches(other, target))).ToArray();

        if (peers.Count(candidate => SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attribute.AttributeClass)) != 1)
        {
            return "duplicate or overlapping descriptions for " + target.ToDisplayString() + "; keep one mapping per target";
        }

        if (attribute.AttributeClass?.Name == "ExternalForwardingAttribute")
        {
            if (!peers.Any(candidate => candidate.AttributeClass?.Name == "ExternalContentAttribute"
                && SymbolEqualityComparer.Default.Equals(Target(candidate), target)))
            {
                return "forwarding requires an ExternalContent description for the same target";
            }

            return DeclarationAnalyzer.ValidateForwarding(target.OriginalDefinition, attribute, compilation, external: true);
        }

        var error = DeclarationAnalyzer.ValidateContent(attribute, compilation, external: true);

        if (error != null)
        {
            return error;
        }

        var hasOutput = !attribute.ConstructorArguments[0].Values.IsEmpty || attribute.NamedArguments.Any(pair =>
            pair.Key == "AllowText" && pair.Value.Value is true
            || pair.Key == "HtmlElements" && !pair.Value.IsNull && !pair.Value.Values.IsEmpty);

        return hasOutput && !attribute.NamedArguments.Any(pair => pair.Key == "Trusted" && pair.Value.Value is true)
            ? "fixed, alternative and default output claims require Trusted = true; they are consumer promises, not verified implementation" : null;
    }
}
