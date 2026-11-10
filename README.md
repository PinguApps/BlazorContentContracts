# PinguApps.BlazorContentContracts

Planned build-time checking of Blazor component content contracts. Component authors will describe permitted components, HTML elements and text; analyzers will check Razor usage, wrappers and forwarded fragments.

The repository contains the solution, annotation assembly, analyzer host, smoke harness and package verification. A minimal static restricted-slot slice reports BCC001 at original Razor tag spans. See [the mapping proof](samples/RazorMapping/README.md) for its explicit consumer setup and limits. No NuGet package has been released.

## Build and verify

Install the SDK selected by `global.json`, then run `dotnet run --file scripts/verify.cs`.
This builds the solution and packs both packages into
`artifacts/packages`, then runs the smoke test and real Razor SDK build scenarios against those packages.
It checks package layouts and release tracking, and builds/runs an
independent .NET 11 consumer restored from those exact packages with an isolated
package cache. The consumer checks analyzer discovery and runtime dependency separation.
Real Razor scenarios also run with SDK 10.0.403 targeting .NET 10. Install both SDKs; when they have separate roots, set `BCC_NET10_DOTNET` to the absolute .NET 10 executable path. CI runs the same command and retains the NuGet artifacts without publishing them.

Both packages target `netstandard2.0`; repository tests and samples target .NET 11. The annotation package has no Roslyn dependency. The analyzer package carries its
assembly under `analyzers/dotnet/cs` and excludes compiler dependencies and runtime
assets. Automatic transitive activation remains later work; Razor diagnostics require explicit additional-source setup.
BCC001 checks direct component symbols, base classes, open and closed generic families, and inherited slot contracts in the qualified static slice. HTML containers and their descendants are checked independently of text permission; BCC005 rejects forbidden HTML/text and BCC004 warns for opaque markup. Add future rules directly to
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

- Repository build SDK: .NET SDK `11.0.100`, with later patches in the `11.0.1xx` feature band permitted by `latestPatch`; previews are disabled.
- Consumer framework and Razor: .NET 11 (`net11.0`), ASP.NET Core/Razor 11 using the Razor tooling bundled with the selected SDK. .NET 10 consumers on SDK `10.0.403` remain supported and are verified against the same packed artifacts. .NET 8, .NET 9 and preview SDKs are outside this scope.
- Build analyzer host: Roslyn `5.12` supplied by SDK `11.0.100` (the analyzer retains Roslyn `5.9` dependencies for .NET 10 host compatibility). The analyzer assembly targets `netstandard2.0`, following the existing PinguApps analyzer convention; that assembly target does not promise support for older compiler hosts or consumer frameworks. Keep compiler dependencies separate from the runtime annotation assembly.
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

## Release drafts and package metadata

The PR template's checked categories become labels and Release Drafter groups
merged PRs into release notes, including `dependencies`. Select `patch`, `minor`
or `major` for the intended package compatibility impact; the highest selected
increment wins, with patch as the default. Breaking API or diagnostic behaviour
requires major; additive compatible behaviour uses minor; fixes use patch.
Renovate's `type/*` labels describe dependency updates, not this package's SemVer.

Pushes to `main` and manual dispatch of Release Drafter update a draft with
matching `v`-prefixed names and tags. Drafting never publishes a release or NuGet
package. Before the first stable release, the maintainer must deliberately set
and verify both draft name and tag to `v1.0.0`; the resolver's initial suggestion
is not approval to publish. Preview releases are deliberate manual work: use a
matching name/tag such as `v1.0.0-preview.1` and mark the release as a prerelease.
Do not treat an automatically updated stable draft as a preview, or publish it
without the later release workflow and human first-publication gate. Weekly
publication remains inactive until its separate qualification and approval.

Both packages embed this README and the MIT licence, with PinguApps author and
repository metadata. The .NET SDK supplies GitHub SourceLink; deterministic
builds embed untracked sources and CI normalizes source paths. Portable symbols
are produced as `.snupkg` artifacts; the analyzer also carries its PDB beside its
assembly under `analyzers/dotnet/cs`, without adding runtime assets or compiler
dependencies. These settings preserve the existing framework/compiler matrix.

## Dependency maintenance

Renovate uses the shared `local>PinguApps/renovate-config` preset, including
reviewers, dependency labels, release-age delays and GitHub Action SHA pinning.
Its [Dependency Dashboard](https://github.com/PinguApps/BlazorContentContracts/issues/3)
confirms hosted access and detection of central NuGet versions, `global.json`
(through the NuGet manager), and GitHub Actions. No separate workflow is required.
Default manager discovery remains enabled for future toolchain files.

Local rules allow newer SDK and compiler versions to appear in the dashboard,
including newer SDK feature bands and .NET major versions.
Updates outside the qualified SDK `11.0.1xx` band (including bundled Razor tooling)
or Roslyn `5.9.x` baseline require Dependency Dashboard approval before Renovate
creates a PR; patch updates within those baselines follow the shared policy.
Approve the dashboard entry to request an upgrade PR, then qualify the
version-specific Razor sample and real consumers before merging it. Update these
approval rules when qualifying a new baseline. Using a newer build SDK does not
by itself change the supported consumer target framework. Ordinary test dependencies and pinned Actions
continue to receive update proposals under the shared policy; no broad automerge
is enabled. The two own-package references are maintained with local package
artifacts and release versions, so Renovate does not look them up on NuGet.org.
Samples and tests remain discoverable rather than receiving blanket exclusions.

The planned weekly publishing channel includes dependency changes merged into
`main`. Open or unmerged Renovate PRs are not included. Publishing setup and
activation remain separate release work.

## Diagnostics

| ID | Default | Meaning and action |
| --- | --- | --- |
| BCC001 | Error | Disallowed component; use an allowed component. |
| BCC002 | Error | Invalid declaration; correct the indicated contract argument or target. |
| BCC003 | Error | Conflicting inherited contract; preserve the base contract. |
| BCC004 | Warning | Opaque markup; replace it with inspectable markup. |
| BCC005 | Error | Disallowed HTML or visible text; adjust the content or contract. |
| BCC006 | Warning | Opaque fragment/method output, runtime component selection or unsupported builder helper; the message explains the uncertainty. Inline inspectable output where possible. |

Unknown output does not hide definite violations in siblings or other branches.
BCC006 is limited to one warning per restricted slot usage. Explicitly allowed
components remain terminal boundaries, including DynamicComponent when allowed.
These diagnostics run at compile time and imply no runtime enforcement.

Use standard analyzer configuration in the consumer's `.globalconfig` (global
settings also apply to diagnostics located in original Razor files):

```ini
is_global = true
dotnet_diagnostic.BCC006.severity = error
dotnet_diagnostic.BCC004.severity = warning
```

Standard values such as `none`, `warning` and `error` also apply to the other IDs.

## Public contract declarations

`AllowedChildren`, `ProducesContent` and `ForwardsContent` describe fragment inputs,
fixed output and transparent forwarding. See [metadata schema v1](docs/contract-schema.md)
for supported targets, defaults, examples and inheritance rules. BCC002 validates
declarations and BCC003 rejects conflicting inherited declarations without executing
application code. Output implementation validation and composition remain later work.
