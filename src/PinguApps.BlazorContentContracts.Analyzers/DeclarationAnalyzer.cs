using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PinguApps.BlazorContentContracts.Analyzers;

/// <summary>Validates schema v1 declarations without loading or executing consumer code.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeclarationAnalyzer : DiagnosticAnalyzer
{
    private const string Prefix = "PinguApps.BlazorContentContracts.";
    private static readonly DiagnosticDescriptor Invalid = new(
        "BCC002", "Invalid content contract declaration", "Invalid content contract on '{0}': {1}",
        "ContentContracts", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor Conflict = new(
        "BCC003", "Conflicting inherited content contract", "Content contract on '{0}' conflicts with inherited declaration: {1}",
        "ContentContracts", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Invalid, Conflict);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType, SymbolKind.Property, SymbolKind.Method);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;

        foreach (var name in new[] { "AllowedChildrenAttribute", "ProducesContentAttribute", "ForwardsContentAttribute" })
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var attribute = Attribute(symbol, name, context.Compilation);
            var inherited = Inherited(symbol, name, context.Compilation);

            if (attribute != null)
            {
                var error = ValidateTarget(symbol, name, context.Compilation);

                if (error == null)
                {
                    error = name == "ForwardsContentAttribute"
                        ? ValidateForwarding((INamedTypeSymbol)symbol, attribute, context.Compilation)
                        : ValidateContent(attribute, context.Compilation);
                }

                if (error != null)
                {
                    Report(context, Invalid, symbol, attribute, error);
                }

                if (inherited != null && !Equivalent(attribute, inherited))
                {
                    Report(context, Conflict, symbol, attribute, "preserve the inherited contract or remove the redeclaration");
                }
            }
            else if (name == "ForwardsContentAttribute" && symbol is INamedTypeSymbol type && inherited != null)
            {
                var error = ValidateForwarding(type, inherited, context.Compilation);

                if (error != null)
                {
                    Report(context, Invalid, symbol, null, error);
                }
            }
            else if (name == "AllowedChildrenAttribute" && symbol is IPropertySymbol && inherited != null)
            {
                var error = ValidateTarget(symbol, name, context.Compilation);

                if (error != null)
                {
                    Report(context, Invalid, symbol, null, error);
                }
            }
        }
    }

    private static string? ValidateTarget(ISymbol symbol, string name, Compilation compilation)
    {
        if (name == "AllowedChildrenAttribute")
        {
            return symbol is IPropertySymbol property && !property.IsStatic && !property.IsIndexer
                && property.DeclaredAccessibility == Accessibility.Public
                && IsComponent(property.ContainingType, compilation) && IsFragment(property.Type, compilation)
                && HasParameter(property, compilation)
                ? null : "AllowedChildren requires a public instance [Parameter] RenderFragment or RenderFragment<T> property on an IComponent";
        }

        if (symbol is INamedTypeSymbol type)
        {
            return type.TypeKind == TypeKind.Class && IsComponent(type, compilation)
                ? null : "component output and forwarding declarations require an IComponent class";
        }

        return name == "ProducesContentAttribute" && symbol is IMethodSymbol method
            && method.MethodKind == MethodKind.Ordinary && IsFragment(method.ReturnType, compilation)
            ? null : "ProducesContent requires an IComponent class or a method returning RenderFragment or RenderFragment<T>";
    }

    private static string? ValidateContent(AttributeData attribute, Compilation compilation)
    {
        if (attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].IsNull)
        {
            return "supply a non-null component type array (use an empty array for no components)";
        }

        foreach (var value in attribute.ConstructorArguments[0].Values)
        {
            if (value.Value is not INamedTypeSymbol type || type.TypeKind != TypeKind.Class
                || !IsComponent(type, compilation) || (!type.IsUnboundGenericType && ContainsParameter(type)))
            {
                return "component types must be IComponent classes, closed constructions, or unbound generic definitions";
            }
        }

        foreach (var argument in attribute.NamedArguments.Where(argument => argument.Key == "HtmlElements"))
        {
            if (argument.Value.IsNull || argument.Value.Values.Any(value => value.Value is not string name
                || !Regex.IsMatch(name, @"\A[A-Za-z][A-Za-z0-9._-]*\z")))
            {
                return "HtmlElements must be a non-null array of tag names such as 'div' or 'my-element', without markup or whitespace";
            }
        }

        return null;
    }

    private static string? ValidateForwarding(INamedTypeSymbol type, AttributeData attribute, Compilation compilation)
    {
        if (attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].IsNull
            || attribute.ConstructorArguments[0].Values.IsEmpty)
        {
            return "ForwardsContent must name at least one fragment parameter";
        }

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in attribute.ConstructorArguments[0].Values)
        {
            if (value.Value is not string name || string.IsNullOrWhiteSpace(name) || !names.Add(name))
            {
                return "forwarded parameter names must be non-empty and unique";
            }

            var member = Member(type, name);

            if (member is not IPropertySymbol property || property.IsStatic || property.IsIndexer
                || property.DeclaredAccessibility != Accessibility.Public || !IsFragment(property.Type, compilation)
                || !HasParameter(property, compilation))
            {
                return "forwarded parameter '" + name + "' must resolve to a public instance [Parameter] RenderFragment or RenderFragment<T> property";
            }
        }

        return null;
    }

    private static ISymbol? Member(INamedTypeSymbol? type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            var members = type.GetMembers(name);

            if (members.Length != 0)
            {
                return members[0];
            }
        }

        return null;
    }

    private static bool HasParameter(IPropertySymbol property, Compilation compilation)
    {
        for (IPropertySymbol? current = property; current != null; current = current.OverriddenProperty)
        {
            if (current.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass,
                compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.ParameterAttribute"))))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFragment(ITypeSymbol type, Compilation compilation)
    {
        return SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.RenderFragment"))
            || type is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition,
                compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.RenderFragment`1"));
    }

    private static bool IsComponent(INamedTypeSymbol type, Compilation compilation)
    {
        return (type.IsUnboundGenericType ? type.OriginalDefinition : type).AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item,
            compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.IComponent")));
    }

    private static bool ContainsParameter(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.TypeParameter || type is INamedTypeSymbol named
            && (named.TypeArguments.Any(ContainsParameter) || named.ContainingType != null && ContainsParameter(named.ContainingType));
    }

    private static AttributeData? Attribute(ISymbol symbol, string name, Compilation compilation)
    {
        var expected = compilation.GetTypeByMetadataName(Prefix + name);

        return expected == null ? null : symbol.GetAttributes().FirstOrDefault(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, expected));
    }

    private static AttributeData? Inherited(ISymbol symbol, string name, Compilation compilation)
    {
        if (symbol is INamedTypeSymbol type)
        {
            for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
            {
                var attribute = Attribute(parent, name, compilation);

                if (attribute != null)
                {
                    return attribute;
                }
            }
        }
        else if (symbol is IPropertySymbol property)
        {
            return property.ContainingType.BaseType is { } parent && Member(parent, property.Name) is { } member
                ? Attribute(member, name, compilation) ?? Inherited(member, name, compilation) : null;
        }
        else if (symbol is IMethodSymbol method && method.OverriddenMethod is { } parent)
        {
            return Attribute(parent, name, compilation) ?? Inherited(parent, name, compilation);
        }

        return null;
    }

    private static bool Equivalent(AttributeData left, AttributeData right)
    {
        return Values(left).SequenceEqual(Values(right), StringComparer.Ordinal);
    }

    private static IEnumerable<string> Values(AttributeData attribute)
    {
        var values = new List<string>();

        if (attribute.ConstructorArguments.Length == 1 && !attribute.ConstructorArguments[0].IsNull)
        {
            values.AddRange(attribute.ConstructorArguments[0].Values.Select(value => "type:" + value.Value));
        }

        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == "AllowText" && argument.Value.Value is true)
            {
                values.Add("text:true");
            }
            else if (argument.Key == "HtmlElements" && !argument.Value.IsNull)
            {
                values.AddRange(argument.Value.Values.Select(value => "html:" + (value.Value as string)?.ToLowerInvariant()));
            }
        }

        return values.Distinct().OrderBy(value => value, StringComparer.Ordinal);
    }

    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, ISymbol symbol,
        AttributeData? attribute, string message)
    {
        var location = attribute?.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
            ?? symbol.Locations.FirstOrDefault();
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, symbol.Name, message));
    }
}
