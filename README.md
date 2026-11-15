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
@using Microsoft.AspNetCore.Components
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

An unaccepted Razor component cannot hide forbidden output either. Define `Bad.razor`:

```razor
<button>Not a menu item</button>
```

This also fails with **BCC005**:

```razor
<Menu>
    <Bad />
</Menu>
```

The analyzer opens `Bad`, finds its forbidden button/text and reports the problem at the usage. This is an output contract: an unlisted component is inspected, rather than rejected solely because its type is absent. A component that emits only permitted output can still pass, as the wrapper examples below show.

Empty content is valid. Whitespace and Razor comments do not count as visible text. Contracts do not require a parent, a minimum number of children or a particular runtime parameter combination.

## Accepted components stop inspection

`Menu` permits `MenuItem`. The analyzer accepts that component as a whole. Its internal `<a>` and `Home` text are allowed even though the menu contract does not permit arbitrary HTML or text:

```razor
<Menu>
    <MenuItem /> @* Accepted: MenuItem is explicitly permitted. *@
</Menu>
```

Suppose you extend `MenuItem.razor` to accept unrestricted content:

```razor
@using Microsoft.AspNetCore.Components

<a href="/">@ChildContent</a>

@code {
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
```

The menu still stops at `MenuItem`, including its supplied child content:

```razor
<Menu>
    <MenuItem>
        <div>Custom item content</div>
        <Bad />
    </MenuItem>
</Menu>
```

**Accepted by the menu contract.** This is useful when a menu item owns its own rendering and customisation rules. It also means that permitting a component is a deliberate promise: the menu does not police everything inside that component.

To restrict the item's content too, give **its own** fragment parameter a contract:

```razor
@code {
    [Parameter, AllowedChildren(HtmlElements = new[] { "strong" }, AllowText = true)]
    public RenderFragment? ChildContent { get; set; }
}
```

Now `<MenuItem><strong>Home</strong></MenuItem>` passes. `<MenuItem><Bad /></MenuItem>` fails with BCC005 under the **item's** contract. The enclosing menu's acceptance does not disable checks belonging to other restricted slots.

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

## Accepted HTML still checks its contents

HTML permissions work differently from accepted component types. Allowing `<div>` permits that tag; its children must independently fit the same contract.

For example, create `GroupedMenu.razor`:

```razor
@using Microsoft.AspNetCore.Components
@using PinguApps.BlazorContentContracts

@ChildContent

@code {
    [Parameter, AllowedChildren(typeof(MenuItem), HtmlElements = new[] { "div" })]
    public RenderFragment? ChildContent { get; set; }
}
```

This passes: each div is allowed, and inspection stops at each accepted menu item.

```razor
<GroupedMenu>
    <div>
        <MenuItem />
        <div><MenuItem /></div>
    </div>
</GroupedMenu>
```

Each of these fails with BCC005:

```razor
<GroupedMenu><div><button /></div></GroupedMenu>
@* The div is allowed; the nested button is not. *@

<GroupedMenu><div><Bad /></div></GroupedMenu>
@* Bad is expanded and its forbidden output is found. *@

<GroupedMenu><div>A heading</div></GroupedMenu>
@* AllowText defaults to false, including inside allowed HTML. *@
```

To permit the last example, set `AllowText = true`. That permits visible text; it does not grant permission for buttons or other HTML elements.

| Encountered content | What happens next |
| --- | --- |
| Explicitly accepted component, or a derived component | Accept it and stop expanding its output for this receiving contract. |
| Accepted HTML tag | Accept the tag, then check everything inside it. |
| Other inspectable component | Expand its output and check that output against the receiving contract. |
| Unavailable or otherwise opaque output | Report uncertainty rather than assume it is valid. |

## Extract wrappers without losing checking

Create `MenuGroup.razor`:

```razor
<MenuItem />
<MenuItem />
```

It does not have to appear in `Menu`'s accepted type list:

```razor
<Menu>
    <MenuGroup />
</Menu>
```

**Accepted.** `MenuGroup` is expanded, and both components it emits are accepted menu items.

Changing the wrapper to this makes the same usage fail with BCC005:

```razor
<div>
    <MenuItem />
</div>
```

The wrapper added a div that `Menu` does not permit. If you deliberately add `typeof(MenuGroup)` to the menu contract, the wrapper becomes an accepted component and inspection stops there instead. Do that only when accepting the wrapper's complete output is what you intend.

Forwarding works too. Create `MenuItems.razor`:

```razor
@using Microsoft.AspNetCore.Components

@ChildContent

@code {
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
```

```razor
<Menu><MenuItems><MenuItem /></MenuItems></Menu> @* Accepted. *@
<Menu><MenuItems><Bad /></MenuItems></Menu>      @* BCC005. *@
```

The wrapper does not hide the supplied fragment. Each emitted template contributes output; an unused fragment parameter does not.

## Check every possible branch

```razor
<Menu>
    @if (showItem)
    {
        <MenuItem />
    }
    else
    {
        <Bad />
    }
</Menu>

@code {
    private bool showItem = true;
}
```

**BCC005**, even though `showItem` starts as true. The analyzer checks possible output rather than executing the application. The same rule applies to switch alternatives, loop bodies, loading/error/empty templates and fragment assignments. A false branch cannot be used to hide a violation.

## Generics, named slots and composition

### Open and closed generic component types

Create `Row.razor`:

```razor
@typeparam TItem

<tr><td>@Value</td></tr>

@code {
    [Parameter]
    public TItem? Value { get; set; }
}
```

In `Rows.razor`, use an open generic to allow every construction:

```razor
@ChildContent

@code {
    [Parameter, AllowedChildren(typeof(Row<>))]
    public RenderFragment? ChildContent { get; set; }
}
```

Both rows are accepted, and their internal HTML is not expanded:

```razor
<Rows>
    <Row TItem="int" Value="42" />
    <Row TItem="string" Value='@("Hello")' />
</Rows>
```

Change the contract to `AllowedChildren(typeof(Row<int>))`:

```razor
<Rows><Row TItem="int" Value="42" /></Rows>          @* Accepted. *@
<Rows><Row TItem="string" Value='@("Hello")' /></Rows> @* BCC005; output may also warn BCC004. *@
```

The unaccepted string row is expanded: its tr/td HTML violates the contract (BCC005), and its runtime `Value` output can additionally be opaque (BCC004). The accepted integer row stops expansion and does not produce those findings.

Derived types are accepted too. A `FeaturedRow : Row<int>` satisfies the closed `Row<int>` contract; a component with the same short name from a different namespace does not gain that permission. An unaccepted type's inspectable output is expanded normally.

### Different rules for different slots

Create `PageSection.razor`:

```razor
@Header
@Body

@code {
    [Parameter, AllowedChildren(HtmlElements = new[] { "h2" }, AllowText = true)]
    public RenderFragment? Header { get; set; }

    [Parameter, AllowedChildren(typeof(MenuItem))]
    public RenderFragment? Body { get; set; }
}
```

This passes:

```razor
<PageSection>
    <Header><h2>Navigation</h2></Header>
    <Body><MenuItem /></Body>
</PageSection>
```

This fails with BCC005 in the body, even though the same HTML is valid in the header:

```razor
<PageSection>
    <Header><h2>Navigation</h2></Header>
    <Body><h2>Wrong slot</h2></Body>
</PageSection>
```

Contracts can also be applied to `RenderFragment<T>`. For example, a row template can receive a value while still emitting only menu items:

```razor
@Template?.Invoke(42)

@code {
    [Parameter, AllowedChildren(typeof(MenuItem))]
    public RenderFragment<int>? Template { get; set; }
}
```

Save that component as `RowTemplate.razor`. These callers show both outcomes:

```razor
<RowTemplate>
    <Template Context="value"><MenuItem /></Template>
</RowTemplate>
@* Accepted: the context value does not change the permitted output. *@

<RowTemplate>
    <Template Context="value"><Bad /></Template>
</RowTemplate>
@* BCC005 in the second template. *@
```

### Forwarded permissions must fit the receiving slot

An annotated wrapper can preserve its input promise when forwarding content:

```razor
<Menu ChildContent="@ChildContent" />

@code {
    [Parameter, AllowedChildren(typeof(MenuItem))]
    public RenderFragment? ChildContent { get; set; }
}
```

This declaration fits the receiving menu contract. Calling the wrapper with `<MenuItem />` passes; calling it with `<Bad />` fails at its own restricted parameter.

If you widen the wrapper parameter to this, forwarding it into the same menu reports **BCC009**:

```csharp
[Parameter, AllowedChildren(typeof(MenuItem), typeof(Bad))]
public RenderFragment? ChildContent { get; set; }
```

The wrapper promises callers it can accept `Bad`, while the receiving menu permits only `MenuItem`. That mismatch exists even if a particular caller currently supplies only menu items. Narrow the source promise or deliberately widen the receiver.

Inherited parameter contracts remain in force. A derived component cannot replace a base restriction with a conflicting one; that reports BCC003.

### Describe output from libraries

`ProducesContent` describes fixed component or fragment-factory output; `ForwardsContent` names emitted fragment parameters. Available implementations are checked against their declarations. For example, this fragment factory promises menu-item output. Put it in the same namespace as `MenuItem`; replace `YourApp.Components` below with that namespace:

```csharp
using Microsoft.AspNetCore.Components;
using PinguApps.BlazorContentContracts;

namespace YourApp.Components;

public static class MenuFragments
{
    [ProducesContent(typeof(MenuItem))]
    public static RenderFragment Home()
    {
        return builder =>
        {
            builder.OpenComponent<MenuItem>(0);
            builder.CloseComponent();
        };
    }
}
```

```razor
<Menu>@MenuFragments.Home()</Menu> @* Accepted. *@
```

If the available implementation instead emits a button, it contradicts its own declaration and reports **BCC010**. Compiled declarations are trusted at call sites; keep the promises accurate, including loading, empty and failure output. Supported inferred summaries also preserve inspectable output through project and NuGet references without hand-authoring attributes.

For an explicit forwarding declaration, `PublishedMenuItems.razor` can promise that it emits no fixed content and forwards `ChildContent`:

```razor
@attribute [ProducesContent]
@attribute [ForwardsContent("ChildContent")]

@ChildContent

@code {
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
```

```razor
<Menu><PublishedMenuItems><MenuItem /></PublishedMenuItems></Menu> @* Accepted. *@
<Menu><PublishedMenuItems><Bad /></PublishedMenuItems></Menu>      @* BCC005. *@
```

The forwarding declaration preserves checking of the supplied content across a compiled library. It does not grant permission for arbitrary descendants.

Consumer-owned `ExternalContent` and `ExternalForwarding` can describe third-party components. Nonempty fixed output claims require `Trusted = true`; review all possible output before making that promise.

See the [contract schema and examples](https://github.com/PinguApps/BlazorContentContracts/blob/main/docs/contract-schema.md) and [buildable adoption examples](https://github.com/PinguApps/BlazorContentContracts/tree/main/samples/Adoption) for declarations, wrappers, forwarding and external components.

## Understand unknown output

Unknown output is different from a proven violation. `MarkupString` reports BCC004; opaque fragments, runtime-selected components and unsupported rendering helpers report BCC006. Uncertainty does not hide known invalid content elsewhere in the same slot.

For example, an unannotated fragment received from elsewhere has unknown output:

```razor
<Menu>@Fragment</Menu>

@code {
    [Parameter]
    public RenderFragment? Fragment { get; set; }
}
```

**BCC006 warning.** Add an accurate input contract where you own the parameter, provide inspectable inline content, or review and acknowledge the individual usage using the suppression syntax below.

These two problems remain independent:

```razor
<Menu>
    @Fragment
    <Bad />
</Menu>
```

With the same parameter declaration, this reports the **BCC006 warning and a BCC005 error**. Suppressing the unknown fragment does not make the known-invalid `Bad` output acceptable.

Enabling text does not make raw markup inspectable:

```razor
<InlineContent>
    @((MarkupString)"<button>Raw markup</button>")
</InlineContent>
```

**BCC004 warning**, despite `InlineContent` allowing visible text. Raw markup could contain HTML outside the permitted tags.

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
