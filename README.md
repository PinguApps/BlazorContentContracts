# PinguApps.BlazorContentContracts

Planned build-time checking of Blazor component content contracts. Component authors will describe permitted components, HTML elements and text; analyzers will check Razor usage, wrappers and forwarded fragments.

This repository currently contains the agreed setup baseline only. No library, analyzer or NuGet package has been implemented or released.

## Repository and release baseline

- Repository: https://github.com/PinguApps/BlazorContentContracts
- Default branch: `main`.
- Licence: MIT; copyright PinguApps.
- Planned package IDs: `PinguApps.BlazorContentContracts` and `PinguApps.BlazorContentContracts.Analyzers`.
- Intended first stable version: `1.0.0` for both packages. This is a release target, not an existing release.

## Initial compatibility targets

These are implementation and qualification targets agreed in PIN-1247, not claims of working diagnostics today.

- Repository build SDK: .NET SDK `10.0.403`, with later patches in the `10.0.4xx` feature band permitted by `latestPatch`; previews are disabled.
- Consumer framework and Razor: .NET 10 (`net10.0`), ASP.NET Core/Razor 10 using the Razor tooling bundled with the selected SDK. .NET 8, .NET 9 and preview SDKs are outside the initial support scope.
- Build analyzer host: Roslyn `5.9` supplied by SDK `10.0.403`. The analyzer assembly targets `netstandard2.0`, following the existing PinguApps analyzer convention; that assembly target does not promise support for older compiler hosts or consumer frameworks. Keep compiler dependencies separate from the runtime annotation assembly.
- Live editor targets: Visual Studio with .NET 10 Razor tooling, and VS Code with the C# extension/C# Dev Kit and .NET 10 tooling. Rider is outside the initial live-editor scope.
- Build diagnostics and original Razor source mapping must be proven in PIN-1249. Editor diagnostics and suppression actions must be proven separately in PIN-1270, recording exact editor, extension, Razor and compiler versions and any limitations. CLI build support does not establish live-editor support.

## Scope of this setup

PIN-1247 establishes the repository and support baseline. The setup follows the `.gitignore`, `.gitattributes`, MIT licence and SDK selection conventions used by [BlazorSitemap](https://github.com/PinguApps/BlazorSitemap) and [CodeStyle.Analyzers](https://github.com/PinguApps/CodeStyle.Analyzers).

The [Linear project](https://linear.app/pinguapps/project/blazorcontentcontracts-d6e2b8fc0fa0) and its [agreed requirements](https://linear.app/pinguapps/document/agreed-scope-and-delivery-requirements-92ad0139880c) remain the delivery plan:

- PIN-1248: solution, annotation/analyzer projects, test harness, CI, packaging and shipped diagnostic tracking.
- PIN-1278: Renovate and the shared PinguApps preset.
- PIN-1279: portfolio package metadata, PR template, labels and release drafting.
- PIN-1271: full documentation and executable examples.
- PIN-1273, PIN-1275 and PIN-1276: release workflow, trusted publishing access and first stable publication.

No publishing credentials are needed for this setup.
