# PinguApps.BlazorContentContracts

[![PinguApps.BlazorContentContracts version](https://img.shields.io/nuget/v/PinguApps.BlazorContentContracts?style=for-the-badge&label=PinguApps.BlazorContentContracts)](https://www.nuget.org/packages/PinguApps.BlazorContentContracts/) [![PinguApps.BlazorContentContracts downloads](https://img.shields.io/nuget/dt/PinguApps.BlazorContentContracts?style=for-the-badge&label=downloads)](https://www.nuget.org/packages/PinguApps.BlazorContentContracts/)

[![PinguApps.BlazorContentContracts.Analyzers version](https://img.shields.io/nuget/v/PinguApps.BlazorContentContracts.Analyzers?style=for-the-badge&label=PinguApps.BlazorContentContracts.Analyzers)](https://www.nuget.org/packages/PinguApps.BlazorContentContracts.Analyzers/) [![PinguApps.BlazorContentContracts.Analyzers downloads](https://img.shields.io/nuget/dt/PinguApps.BlazorContentContracts.Analyzers?style=for-the-badge&label=downloads)](https://www.nuget.org/packages/PinguApps.BlazorContentContracts.Analyzers/)

**Declare what belongs inside your Blazor components and catch invalid content when you build.** A menu can accept only menu items; a table slot can accept only rows; a text slot can allow selected HTML tags. Content contracts make these expectations explicit and report mistakes at the original Razor usage, before they reach your users.

Checking follows local wrappers, forwarded fragments and supported compiled library contracts. You can extract reusable components without losing the rules on the content they produce. Known violations are errors; output the analyzer cannot establish produces warnings so you can review it.

For **.NET 10 and .NET 11 Blazor**. Checking runs at compile time, with no runtime enforcement or security guarantee.

## Installation

Install in the Blazor app or component library that declares or uses contracts:

```sh
dotnet add package PinguApps.BlazorContentContracts --version 1.0.0
```

The main package supplies the attributes and activates the analyzer automatically, including through component-library dependencies. No separate analyzer installation or service registration is required. Keep the default flowing package assets; see [activation and opt-outs](https://github.com/PinguApps/BlazorContentContracts/blob/main/docs/package-activation.md) for custom asset policies.

Add the namespace to `_Imports.razor`:

```razor
@using PinguApps.BlazorContentContracts
```

## Accept only specific components

Create `Menu.razor` with a restricted `ChildContent` parameter:

```razor
@using Microsoft.AspNetCore.Components
@using PinguApps.BlazorContentContracts

<nav>@ChildContent</nav>

@code {
    [Parameter, AllowedChildren(typeof(MenuItem))]
    public RenderFragment? ChildContent { get; set; }
}
```

For this example, `MenuItem.razor` renders a link:

```razor
<a href="/">Home</a>
```

This works:

```razor
<Menu>
    <MenuItem />
    <MenuItem />
</Menu>
```

These usages fail with BCC005:

```razor
<Menu>
    <div>Not a menu item</div>
</Menu>

<Menu>
    Plain text is not a menu item
</Menu>
```

`MenuItem` and its derived components are accepted as boundaries: their own HTML and text are not expanded. Other inspectable components are expanded and their output must fit the contract. A local wrapper that emits only `MenuItem` works; a wrapper that adds a `div` does not.

Empty content is valid. Whitespace and Razor comments do not count as visible text. Contracts do not require a parent, a minimum number of children or a particular runtime parameter combination.

## Allow selected HTML and text

Create `InlineContent.razor`:

```razor
@using Microsoft.AspNetCore.Components
@using PinguApps.BlazorContentContracts

@ChildContent

@code {
    [Parameter, AllowedChildren(HtmlElements = new[] { "span", "strong" }, AllowText = true)]
    public RenderFragment? ChildContent { get; set; }
}
```

This works:

```razor
<InlineContent>
    Hello <span><strong>world</strong></span>
</InlineContent>
```

This fails with BCC005 because allowing a container does not allow arbitrary descendants:

```razor
<InlineContent>
    <span><div>Still forbidden</div></span>
</InlineContent>
```

`HtmlElements` defaults to empty and matches names without case sensitivity. `AllowText` defaults to `false`, including inside permitted HTML. You can combine HTML/text permissions with component types in the same attribute.

## Generics, named slots and composition

- `AllowedChildren(typeof(Row<>))` accepts every constructed `Row<T>` and its derived components. `typeof(Row<int>)` accepts that construction and its subclasses, but not `Row<string>`.
- Apply a contract to each named `RenderFragment` or `RenderFragment<T>` parameter independently. Inherited parameter contracts remain in force; conflicting redeclarations report BCC003.
- Every possible branch, loop body and emitted template is checked. A currently false branch does not make invalid content acceptable.
- Annotated fragments keep their permissions when forwarded. Those permissions must fit entirely inside the receiving contract (BCC009).
- `ProducesContent` describes fixed component or fragment-factory output; `ForwardsContent` describes emitted fragment parameters. Available implementations are checked against these promises. Supported compiled summaries preserve output across project and NuGet references.
- Consumer-owned `ExternalContent` and `ExternalForwarding` attributes can describe third-party components. Nonempty fixed output claims require explicit `Trusted = true` and review of the component's possible output.

See the [contract schema and examples](https://github.com/PinguApps/BlazorContentContracts/blob/main/docs/contract-schema.md) and [buildable adoption examples](https://github.com/PinguApps/BlazorContentContracts/tree/main/samples/Adoption) for declarations, wrappers, forwarding and external components.

## Understand unknown output

Unknown output is different from a proven violation. `MarkupString` reports BCC004; opaque fragments, runtime-selected components and unsupported rendering helpers report BCC006. Uncertainty does not hide known invalid content elsewhere in the same slot.

The analyzer does not execute application code or sanitize HTML. Declared output in unavailable implementations remains an author promise. See the [adoption guide](https://github.com/PinguApps/BlazorContentContracts/blob/main/docs/adoption.md) for framework templates, mutation and trust boundaries, and the suppression syntax below for one reviewed usage.

## Initial compatibility targets

- **Build checking:** .NET 10 with SDK 10.0.403 and .NET 11 with SDK 11.0.100 (later patches in 11.0.1xx permitted). .NET 8, .NET 9 and preview SDKs are outside the qualified matrix.
- **Live editor feedback:** Visual Studio 18.11 and VS Code 1.139 with .NET 10 consumers. Rider and .NET 11 live editing are not qualified. See [editor support](https://github.com/PinguApps/BlazorContentContracts/blob/main/docs/editor-support.md) for exact extensions, save/rebuild boundaries and the opt-in VS Code suppression action.
- **Compiler hosts:** Roslyn 5.9 on .NET 10 and 5.12 on .NET 11. Both packages target netstandard2.0; this does not imply support for older compiler hosts. Compiler dependencies stay separate from the runtime annotation assembly.

The [framework catalogue](https://github.com/PinguApps/BlazorContentContracts/blob/main/docs/framework-catalogue.md) explains supported framework components, including authorization alternatives, error fallbacks, virtualization and relocated content.

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
| BCC012 | Warning | Recursive wrapper or fragment output; break the cycle or declare an accurate output contract. |
| BCC013 | Warning | Analysis depth or work limit reached; simplify output or declare an accurate output contract. |
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
`AdditionalFiles` automatically; see [package activation](https://github.com/PinguApps/BlazorContentContracts/blob/main/docs/package-activation.md)
for asset policies and opt-outs. Standard C# suppression/configuration remains available
for diagnostics with C# locations; a C# pragma does not cover an external Razor
diagnostic span.

The executable Gherkin fixtures in [RazorSuppressions.feature](https://github.com/PinguApps/BlazorContentContracts/blob/main/tests/PinguApps.BlazorContentContracts.Tests/Features/RazorSuppressions.feature) exercise
real `.razor` consumers on .NET 10/11 with the packed artifacts.
[Suppression.razor](https://github.com/PinguApps/BlazorContentContracts/blob/main/samples/RazorMapping/Suppression.razor) is a reusable editor
fixture: removing either comment should restore its unknown-output diagnostic;
duplicating the expression should leave the sibling checked. Build evidence alone does not establish live-editor support. See
[editor qualification](https://github.com/PinguApps/BlazorContentContracts/blob/main/docs/editor-support.md) for separate host evidence
and suppression-action limitations.

## Public contract declarations

Consumers can describe unmodified third-party wrappers locally with assembly-level
`ExternalContent` and `ExternalForwarding` attributes. Forwarded descendants remain
checked. Fixed/default output claims require explicit `Trusted = true`; mappings
bind exact symbols and do not extend to derived rendering overrides. See
[external descriptions](https://github.com/PinguApps/BlazorContentContracts/blob/main/docs/contract-schema.md#consumer-descriptions-of-external-components)
for generic support, precedence and the consumer trust boundary.

`AllowedChildren`, `ProducesContent` and `ForwardsContent` describe fragment inputs,
fixed output and transparent forwarding. See [metadata schema v1](https://github.com/PinguApps/BlazorContentContracts/blob/main/docs/contract-schema.md)
for supported targets, defaults, examples and inheritance rules. BCC002 validates
declarations and BCC003 rejects conflicting inherited declarations without executing
application code. Local wrapper composition follows every emitted template, including alternative and typed templates. Unused templates do not contribute output. Findings include the wrapper path. Explicit output contracts are checked against available implementations and trusted at calls, including referenced assemblies. Unavailable implementations remain an author promise with no runtime guarantee; bounded inferred summaries now survive project and NuGet references. See [compiled summary schema v1](https://github.com/PinguApps/BlazorContentContracts/blob/main/docs/contract-schema.md#compiled-summary-schema-v1) for supported output and build costs. BCC014 warns for incompatible compiled metadata.

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
