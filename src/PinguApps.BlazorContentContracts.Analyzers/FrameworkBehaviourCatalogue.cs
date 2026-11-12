using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PinguApps.BlazorContentContracts.Analyzers;

internal static class FrameworkBehaviourCatalogue
{
    // Assembly versions are independent of SDK/compiler versions. Patch releases
    // keep these assembly identities; new major/minor identities require review.
    internal static FrameworkOutputDescription? Find(INamedTypeSymbol component, Compilation compilation, out string? uncertainty)
    {
        uncertainty = null;

        for (var current = component; current != null; current = current.BaseType)
        {
            var definition = current.OriginalDefinition;
            var name = definition.ContainingNamespace.ToDisplayString() + "." + definition.MetadataName;
            var description = Describe(name, out var assemblyName);

            if (description == null)
            {
                continue;
            }

            var identity = definition.ContainingAssembly.Identity;

            if (identity.Name != assemblyName || identity.PublicKeyToken.Length != 8
                || !identity.PublicKeyToken.SequenceEqual(new byte[] { 0xad, 0xb9, 0x79, 0x38, 0x29, 0xdd, 0xae, 0x60 })
                || !SymbolEqualityComparer.Default.Equals(definition, compilation.GetTypeByMetadataName(name)))
            {
                continue;
            }

            if (identity.Version != new Version(10, 0, 0, 0) && identity.Version != new Version(11, 0, 0, 0))
            {
                uncertainty = $"framework assembly {identity.Name} {identity.Version} has no qualified behaviour description; use a supported framework version or an explicit external output description";
                return null;
            }

            // Even an apparently empty derived type can change lifecycle, state,
            // fragment accessors or IComponent dispatch. Do not inherit exemptions.
            if (!SymbolEqualityComparer.Default.Equals(component.OriginalDefinition, definition))
            {
                uncertainty = "derived framework component behaviour is not qualified; declare its output explicitly or describe the external component";
                return null;
            }

            if (name == "Microsoft.AspNetCore.Components.Forms.EditForm" && identity.Version.Major == 11)
            {
                return new("conditional-elements-unresolved", new[] { "ChildContent" }, new[] { "form", "input" });
            }

            return description;
        }

        return null;
    }

    private static FrameworkOutputDescription? Describe(string name, out string assembly)
    {
        assembly = "Microsoft.AspNetCore.Components";

        switch (name)
        {
            case "Microsoft.AspNetCore.Components.CascadingValue`1":
                return new("forwarding", new[] { "ChildContent" }, Array.Empty<string>());
            case "Microsoft.AspNetCore.Components.Sections.SectionContent":
                return new("relocated", new[] { "ChildContent" }, Array.Empty<string>());
            case "Microsoft.AspNetCore.Components.Sections.SectionOutlet":
                return new("unresolved", Array.Empty<string>(), Array.Empty<string>());
        }

        assembly = "Microsoft.AspNetCore.Components.Authorization";

        switch (name)
        {
            case "Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState":
                return new("forwarding", new[] { "ChildContent" }, Array.Empty<string>());
            case "Microsoft.AspNetCore.Components.Authorization.AuthorizeView":
                return new("alternatives", new[] { "ChildContent", "Authorized", "NotAuthorized", "Authorizing" }, Array.Empty<string>());
        }

        assembly = name == "Microsoft.AspNetCore.Components.Forms.DataAnnotationsValidator"
            ? "Microsoft.AspNetCore.Components.Forms" : "Microsoft.AspNetCore.Components.Web";

        switch (name)
        {
            case "Microsoft.AspNetCore.Components.Forms.InputRadioGroup`1":
            case "Microsoft.AspNetCore.Components.Forms.FormMappingScope":
                return new("forwarding", new[] { "ChildContent" }, Array.Empty<string>());
            case "Microsoft.AspNetCore.Components.Forms.DataAnnotationsValidator":
                return new("empty", Array.Empty<string>(), Array.Empty<string>());
            case "Microsoft.AspNetCore.Components.Forms.EditForm":
                return new("conditional-elements", new[] { "ChildContent" }, new[] { "form", "input" });
        }

        assembly = "Microsoft.AspNetCore.Components.Web";

        switch (name)
        {
            case "Microsoft.AspNetCore.Components.Web.HeadContent":
            case "Microsoft.AspNetCore.Components.Web.PageTitle":
                return new("relocated", new[] { "ChildContent" }, Array.Empty<string>());
            case "Microsoft.AspNetCore.Components.Web.ErrorBoundary":
                return new("defaults", new[] { "ChildContent", "ErrorContent" }, new[] { "div" });
            case "Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize`1":
                return new("parameter-dependent", new[] { "ChildContent", "ItemContent", "Placeholder", "EmptyContent" }, new[] { "div" });
            case "Microsoft.AspNetCore.Components.Web.HeadOutlet":
                return new("unresolved", Array.Empty<string>(), Array.Empty<string>());
            case "Microsoft.AspNetCore.Components.Forms.AntiforgeryToken":
                return new("conditional-elements", Array.Empty<string>(), new[] { "input" });
            case "Microsoft.AspNetCore.Components.Routing.FocusOnNavigate":
                return new("conditional-elements", Array.Empty<string>(), new[] { "blazor-focus-on-navigate" });
            default:
                return null;
        }
    }
}
