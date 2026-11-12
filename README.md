# PinguApps.BlazorContentContracts

Build-time checking of Blazor component content contracts. Component authors describe permitted components, HTML elements and text; analyzers check Razor usage, wrappers and forwarded fragments.

Start with the [adoption guide](docs/adoption.md) and [buildable Razor examples](samples/Adoption). They cover permissions, wrappers, output declarations, framework behaviour, uncertainty and local suppression. Checking provides no runtime or security guarantee. No public NuGet release exists yet; use repository artifacts.

The repository contains the solution, annotation assembly, analyzer host, smoke harness and package verification. Restricted slots analyse local wrapper output at original Razor usage spans. See [the mapping proof](samples/RazorMapping/README.md) for its explicit consumer setup and limits. No NuGet package has been released.

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
assets. Installing the annotation package activates checking automatically, including through library packages and project references. See [package activation](docs/package-activation.md) for asset policy and opt-outs.
Allowed components match symbols, base classes and open or closed generic families, and stop expansion. Local wrappers are expanded recursively; proven-empty components pass and unavailable implementations warn. HTML containers and their descendants are checked independently of text permission; BCC005 rejects forbidden HTML/text and BCC004 warns for opaque markup. Add future rules directly to
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

The [framework behaviour catalogue](docs/framework-catalogue.md) documents dependency
identity/version selection and how to qualify entries, separately from this SDK/compiler host matrix.

The build targets below are checked by package verification. Live support is limited to the separately recorded editor qualification.

- Repository build SDK: .NET SDK `11.0.100`, with later patches in the `11.0.1xx` feature band permitted by `latestPatch`; previews are disabled.
- Consumer framework and Razor: .NET 11 (`net11.0`), ASP.NET Core/Razor 11 using the Razor tooling bundled with the selected SDK. .NET 10 consumers on SDK `10.0.403` remain supported and are verified against the same packed artifacts. .NET 8, .NET 9 and preview SDKs are outside this scope.
- Build analyzer host: Roslyn `5.12` supplied by SDK `11.0.100` (the analyzer retains Roslyn `5.9` dependencies for .NET 10 host compatibility). The analyzer assembly targets `netstandard2.0`, following the existing PinguApps analyzer convention; that assembly target does not promise support for older compiler hosts or consumer frameworks. Keep compiler dependencies separate from the runtime annotation assembly.
- Qualified live editor hosts: Visual Studio 18.11 and VS Code 1.139 with .NET 10 consumers. See [editor qualification](docs/editor-support.md) for exact extension/compiler versions, repeatable fixtures, save/rebuild boundaries and action limitations. Rider and .NET 11 live editing are not qualified.
- CLI build support and live-editor qualification are separate evidence. The editor record covers original Razor spans, wrappers, library contracts and the opt-in VS Code suppression action; Visual Studio insertion remains manual.

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
| BCC001 | Error | Retained shipped diagnostic ID; not currently emitted. Output violations use BCC005 and unresolved output uses BCC006. |
| BCC002 | Error | Invalid declaration; correct the indicated contract argument or target. |
| BCC003 | Error | Conflicting inherited contract; preserve the base contract. |
| BCC004 | Warning | Opaque markup; replace it with inspectable markup. |
| BCC005 | Error | Disallowed HTML or visible text; adjust the content or contract. |
| BCC006 | Warning | Opaque fragment/method output, runtime component selection or unsupported builder helper; the message explains the uncertainty. Inline inspectable output where possible. |
| BCC007 | Warning | Malformed package suppression comment; specify `next`, BCC004 or BCC006, and a reason. |
| BCC008 | Warning | Unused suppression; remove it or move it immediately before the intended usage. |
| BCC009 | Error | Declared or inferred compiled output, or forwarded parameter permissions, exceed the receiving contract; narrow the source permissions or widen the receiver deliberately. |
| BCC010 | Error | Known implementation output or storage write contradicts its ProducesContent or AllowedChildren declaration. |
| BCC011 | Warning | Declared implementation output is opaque; inspect it or acknowledge uncertainty locally with a C# pragma. |
| BCC014 | Warning | Compiled summary or annotation identity/version is incompatible; rebuild with supported annotations or declare output explicitly. |
| BCC015 | Error | Invalid or conflicting external component description; correct the target/slots or explicitly acknowledge trusted output. |

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

### Acknowledge one unknown usage

This is **package syntax**, not a Razor standard. Use a Razor comment immediately
before the component or fragment expression whose unknown output you have reviewed:

```razor
<Slot>
    @* bcc-suppress-next BCC006: supplied by our application *@
    @Fragment
    @* bcc-suppress-next BCC004: trusted markup from our renderer *@
    @((MarkupString)trustedMarkup)
</Slot>
```

Each comment accepts exactly one ID (`BCC004` or `BCC006`) and a nonempty reason
after the colon. The `bcc-suppress*` prefix is reserved for package directives;
use other wording for ordinary comment prose. Matching is case-sensitive. Spaces, tabs and newlines between
the comment and target are allowed; other comments, HTML, text and Razor control
flow interrupt attachment. A component target starts at its opening tag; a
fragment expression starts at `@` (including `@(...)`). For an opaque fragment
passed as a component parameter, put the comment before that component.

Only the specified unknown diagnostic for that usage is suppressed, even when
promoted to an error. Other siblings, other unknown IDs and definite invalid
content remain checked. A comment before a container does not exempt its nested
expressions. Comments add no runtime components and do not change rendering.
Malformed directives report BCC007; valid directives that suppress nothing report
BCC008 at the comment. Missing or ambiguous source mapping leaves the suppression
unused rather than exempting a broader region. Package consumers receive Razor
`AdditionalFiles` automatically; see [package activation](docs/package-activation.md)
for asset policies and opt-outs. Standard C# suppression/configuration remains available
for diagnostics with C# locations; a C# pragma does not cover an external Razor
diagnostic span.

The executable Gherkin fixtures in [RazorSuppressions.feature](tests/PinguApps.BlazorContentContracts.Tests/Features/RazorSuppressions.feature) exercise
real `.razor` consumers on .NET 10/11 with the packed artifacts.
[Suppression.razor](samples/RazorMapping/Suppression.razor) is a reusable editor
fixture: removing either comment should restore its unknown-output diagnostic;
duplicating the expression should leave the sibling checked. Per the agreed
PIN-1255 scope, build evidence alone does not establish live-editor support. See
[editor qualification](docs/editor-support.md) for PIN-1270's separate host evidence
and suppression-action limitations.

## Public contract declarations

Consumers can describe unmodified third-party wrappers locally with assembly-level
`ExternalContent` and `ExternalForwarding` attributes. Forwarded descendants remain
checked. Fixed/default output claims require explicit `Trusted = true`; mappings
bind exact symbols and do not extend to derived rendering overrides. See
[external descriptions](docs/contract-schema.md#consumer-descriptions-of-external-components)
for generic support, precedence and the consumer trust boundary.

`AllowedChildren`, `ProducesContent` and `ForwardsContent` describe fragment inputs,
fixed output and transparent forwarding. See [metadata schema v1](docs/contract-schema.md)
for supported targets, defaults, examples and inheritance rules. BCC002 validates
declarations and BCC003 rejects conflicting inherited declarations without executing
application code. Local wrapper composition follows every emitted template, including alternative and typed templates. Unused templates do not contribute output. Findings include the wrapper path. Explicit output contracts are checked against available implementations and trusted at calls, including referenced assemblies. Unavailable implementations remain an author promise with no runtime guarantee; bounded inferred summaries now survive project and NuGet references. See [compiled summary schema v1](docs/contract-schema.md#compiled-summary-schema-v1) for supported output and build costs. BCC014 warns for incompatible compiled metadata.

Annotated fragment parameters preserve their permissions when forwarded, including typed
and inherited parameters. The source contract must fit entirely inside the receiving
contract: derived component families, open/closed generics, HTML names and text are
compared without executing fragment code. Callers are checked at the source parameter.
Conflicting redeclarations report BCC003 and retain the base restriction. Opaque
unannotated forwarding reports BCC006; annotate its source or supply inline content.

## Bounded fragment assignment analysis

Restricted Razor slots inspect fragment locals, private fields and properties,
including initializers, ordinary/coalescing assignments, `+=`, delegate `+`,
conditional values and inspectable method/getter returns. Typed fragments use the
same rules. Lifecycle and event methods contribute every possible write; this is
an order-independent may-write analysis, so an overwritten invalid value can still
be reported. Local aliases carry known output and uncertainty together.

Opaque service results, captured delegate-factory output, ref/out mutation, delegate removal, externally writable
storage, custom setters and recursive/unavailable sources remain unknown (BCC006,
or BCC011 when checking a declared output implementation). The analyzer does not
execute lifecycle methods or prove runtime ordering, object identity or arbitrary
alias mutation. Known violations remain errors alongside uncertainty. Compiled
output and annotated parameter contracts retain their declared call-site promises.

Recursive wrapper or fragment output reports **BCC012** (warning) with its expansion path.
Break the cycle or declare an accurate output contract. Analysis stops expanding a branch
at 64 active expansion levels or 4096 expansion operations per receiving contract and
reports **BCC013** (warning). Independent known violations remain errors. These warnings
can be promoted using `dotnet_diagnostic.BCC012.severity = error` and
`dotnet_diagnostic.BCC013.severity = error` in analyzer configuration.
Source/body caches live only within one receiving-contract analysis. Render bodies are
keyed by constructed component symbols; fragment sources by syntax identity. Validated
output is never cached across contracts or supplied templates, and edits start fresh.
