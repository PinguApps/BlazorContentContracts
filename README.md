# PinguApps.BlazorContentContracts

Planned build-time checking of Blazor component content contracts. Component authors will describe permitted components, HTML elements and text; analyzers will check Razor usage, wrappers and forwarded fragments.

The repository contains the solution, annotation assembly, analyzer host, smoke harness and package verification. A minimal static restricted-slot slice reports BCC001 at original Razor tag spans. See [the mapping proof](samples/RazorMapping/README.md) for its explicit consumer setup and limits. No NuGet package has been released.

## Build and verify

Install the SDK selected by `global.json`, then run `dotnet run --file scripts/verify.cs`.
This builds the solution and packs both packages into
`artifacts/packages`, then runs the smoke test and real Razor SDK build scenarios against those packages.
It checks package layouts and release tracking, and builds/runs an
independent .NET 10 consumer restored from those exact packages with an isolated
package cache. The consumer checks analyzer discovery and runtime dependency separation.
CI runs the same command and retains the NuGet artifacts without publishing them.

The annotation package has no Roslyn dependency. The analyzer package carries its
assembly under `analyzers/dotnet/cs` and excludes compiler dependencies and runtime
assets. Automatic transitive activation remains later work; Razor diagnostics require explicit additional-source setup.
BCC001 checks exact direct component types in the qualified static slice. Add future rules directly to
`AnalyzerReleases.Shipped.md` under `1.0.0`; `Unshipped` is a comment-only file
retained for Roslyn's release-tracking tooling. Release tracking warnings fail the build.
The verification command disables .NET's automatic global-tools PATH integration
and checks saved Windows user/machine PATH values before and after execution.

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

## Public contract declarations

`AllowedChildren`, `ProducesContent` and `ForwardsContent` describe fragment inputs,
fixed output and transparent forwarding. See [metadata schema v1](docs/contract-schema.md)
for supported targets, defaults, examples and inheritance rules. BCC002 validates
declarations and BCC003 rejects conflicting inherited declarations without executing
application code. Output implementation validation and composition remain later work.
