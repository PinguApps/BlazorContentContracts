# Adopting content contracts

Content contracts check what a Razor fragment may emit at build time. They do
not enforce runtime behaviour, validate services/parameter combinations, sanitize
HTML or provide a security boundary. There are no required parents, minimum
child counts or IntelliSense filtering. Empty content is always valid.

## Install and try

No public package has been released yet. Build local artifacts using the SDKs in
the [support matrix](../README.md#initial-compatibility-targets):

```powershell
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
dotnet run --file scripts/verify.cs
dotnet build samples/Adoption/Adoption.csproj --source ./artifacts/packages
```

Use the absolute path of your installed `dotnet` executable when running these
commands. The environment variable disables automatic global-tools PATH setup;
do not change process or saved PATH. Local artifacts are version 1.0.0, the
intended first stable version, rather than evidence of a public release.
The sample is a Razor component library: build it; it is not a hosted web app.
Framework examples demonstrate compile-time output analysis and require their
normal Blazor services/context if used in a running application.

For adoption in a centrally managed repository, add version 1.0.0 for
`PinguApps.BlazorContentContracts` to `Directory.Packages.props` and add:

```xml
<PackageReference Include="PinguApps.BlazorContentContracts" />
```

Outside central management use `Version="1.0.0"` on that reference, selecting
the actual released version once available. Restore from your local artifact feed
until publication. The annotation dependency activates the analyzer automatically
through library packages and project references. Keep its default flowing assets.
See [activation and opt-outs](package-activation.md) before using PrivateAssets,
IncludeAssets or ExcludeAssets. The analyzer is not a runtime dependency.

## Read the examples

Start at [Branches.razor](../samples/Adoption/Branches.razor), then inspect its
[Slot](../samples/Adoption/Slot.razor), [HTML slot](../samples/Adoption/HtmlSlot.razor)
and [Wrapper](../samples/Adoption/Wrapper.razor).

- `AllowedChildren(typeof(Row<>))` accepts any constructed Row and subclasses.
  `typeof(Row<int>)` accepts that construction and subclasses, not Row<string>.
  Allowed component families are terminal: Row's internal div/text is not expanded.
- `HtmlElements` defaults to empty and matches case-insensitively. Allowed HTML
  remains a container whose descendants must also fit. A span does not grant a
  nested div permission. `AllowText` defaults to false and applies inside HTML too.
  Whitespace and comments are ignored; visible entities and strings count as text.
- The derived slot inherits the base parameter contract. Equivalent redeclarations
  are permitted; conflicting replacements report BCC003 and retain the base rule.
- Every if/switch alternative and loop body contributes possible output, even
  when a branch would not execute with today's data. Named slots and
  `RenderFragment<T>` templates carry their own parameter contracts.
- Local wrappers expand their available render bodies. Every emitted template
  contributes output; unused parameters do not. `ForwardsContent` describes
  forwarded slots; `ProducesContent` describes fixed output. Both contributions
  must fit the receiver. Forwarded annotated parameters must have permissions
  entirely contained in the receiver's permissions (BCC009).
- Put `ProducesContent` on a fragment factory method, not on its return target.
  Available return branches/components are checked (BCC010 for contradictions,
  BCC011 for uncertainty). Compiled declarations are trusted author promises.
  Compile the library with this analyzer and verify that its declaration covers
  every normal, loading, empty and failure path. An inaccurate promise can hide
  unavailable implementation output; it does not make that output safe.

The [schema guide](contract-schema.md) defines exact attribute targets,
inheritance, generic matching, inferred summaries and external-description
precedence. Inferred summaries describe supported static output, not arbitrary
IL. Rebuild referenced libraries after edits; design-time analysis sees their
last normal build. Summary publication can perform a second build pass, including
post-build events. Generated CompiledContent/CompiledForwarding attributes are
not hand-authored output declarations.

## Unknown output and mutation

Invalid means the analyzer found output that contradicts the contract. Unknown
means it could not establish all possible output. Both may occur in the same
slot; uncertainty never erases definite violations. MarkupString is opaque even
with text enabled (BCC004). Opaque factories/fragments, dynamic component types
and unsupported builder helpers produce BCC006.

Fragment locals/private storage include initializers, assignments, coalescing
assignments, delegate addition and possible lifecycle/event writes. This is a
conservative union, not execution-order simulation: overwritten invalid values
can still be reported. Aliases retain known output plus uncertainty. Ref/out,
delegate removal, custom setters, external writes and arbitrary identity/alias
mutation cannot be proven safe. No application code is executed.

BCC012 explains recursion with an expansion path; break the cycle or declare
accurate output. BCC013 explains the 64-active-level or 4096-operation analysis
limit per receiver; simplify/split expansion or supply a trustworthy declaration.
Independent violations remain reported. These warnings can be promoted to errors.

## Framework output

[Framework.razor](../samples/Adoption/Framework.razor) demonstrates the providers,
authorization alternatives, explicit error fallback, virtualization templates and
relocated/empty output. The [versioned catalogue](framework-catalogue.md) records
the exact .NET 10/11 assembly identities and qualification procedure.

| Component family | Contract behaviour |
| --- | --- |
| CascadingValue, CascadingAuthenticationState, InputRadioGroup, FormMappingScope | Forward supplied ChildContent, including typed form context |
| AuthorizeView | Check Authorized or ChildContent, NotAuthorized and Authorizing; do not supply Authorized and ChildContent together |
| ErrorBoundary | Check child and error templates; omitted ErrorContent adds possible div; explicit empty fallback replaces it |
| Virtualize | Check item/child, placeholder and empty templates; default spacers are div; SpacerElement changes spacers only; omitted placeholder still adds div; dynamic spacer is unknown |
| HeadContent, PageTitle, SectionContent | No local output; content is relocated, without whole-app outlet matching |
| DataAnnotationsValidator | Empty local output |
| HeadOutlet, SectionOutlet | Unknown unless explicitly described |
| EditForm | Check child content plus possible form/input; .NET 11 client validation additionally remains unknown |
| AntiforgeryToken | Possible input, even though some runtime contexts emit nothing |
| FocusOnNavigate | Possible blazor-focus-on-navigate under static rendering; analysis does not infer ambient render mode |

Explicitly allowed framework components are terminal like any other allowed
component. Unsupported versions, derived framework types and similarly named
user types do not gain catalogue exemptions.

## Severity and a reviewed usage

The [diagnostic table](../README.md#diagnostics) lists every shipped ID/default.
Use analyzer configuration to change severity globally, for example:

```ini
is_global = true
dotnet_diagnostic.BCC006.severity = error
dotnet_diagnostic.BCC012.severity = error
dotnet_diagnostic.BCC013.severity = error
```

For one reviewed opaque output, insert a Razor comment immediately before the
component or fragment expression:

```razor
@* bcc-suppress-next BCC006: vendor output reviewed for this slot *@
@VendorFragment()
```

Only BCC004/BCC006 support this convention. Include a nonempty reason. It applies
to one immediately following instance, not siblings, descendants independently,
known-invalid diagnostics, cycles or limits. It still works when that warning is
promoted. Malformed comments produce BCC007; unused comments produce BCC008.
[ReviewedMarkup.razor](../samples/Adoption/ReviewedMarkup.razor) compiles a concrete
example. Use local C# pragma disable/restore BCC011 around a reviewed uncertain
implementation; its BCC010 contradictions remain errors. The [editor guide](editor-support.md)
records qualified hosts, save/rebuild boundaries and VS Code's opt-in action.

## Maintaining and upgrading

Keep annotations and analyzers on compatible versions. Qualify new SDK, compiler,
framework and editor hosts separately using real packed consumers; a netstandard2.0
assembly does not promise support for every compiler. Follow the root support
matrix and editor evidence rather than assuming all Blazor versions work.

Breaking public API or diagnostic behaviour requires a major version and release
notes; compatible additions use minor and fixes use patch. Review new/changed
diagnostics and metadata schema compatibility before upgrading, especially with
warnings promoted to errors. Record every new diagnostic directly in
AnalyzerReleases.Shipped.md under its release. Keep Unshipped as the blank tracking
template. Release drafting, publishing and final packed-release qualification
remain separate workflows; building these examples is not release certification.
