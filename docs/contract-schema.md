# Content contract metadata schema v1

The annotation assembly is `PinguApps.BlazorContentContracts`. Its assembly metadata
key `BlazorContentContracts.SchemaVersion` has value `1`. The three public sealed authoring
attributes live in namespace `PinguApps.BlazorContentContracts`, derive directly
from `System.Attribute`, allow one declaration per target and are inherited.
They are retained in compiled metadata; no application code is executed to read them.

| Attribute | Constructor | Supported target |
| --- | --- | --- |
| `AllowedChildrenAttribute` | `params System.Type[] componentTypes` | Public instance, non-indexed `[Parameter]` property on an `IComponent` class, of type `RenderFragment` or `RenderFragment<T>` |
| `ProducesContentAttribute` | `params System.Type[] componentTypes` | `IComponent` class, or ordinary method returning `RenderFragment` or `RenderFragment<T>` |
| `ForwardsContentAttribute` | `params System.String[] parameterNames` | `IComponent` class |

Use `[ProducesContent]` on a method itself, rather than `[return: ProducesContent]`.
Static fragment factory methods are supported. Async factories returning `Task<RenderFragment>`
are outside this schema. C# rejects unsupported attribute targets and duplicate attributes;
the analyzer validates the supported target's semantic requirements.

Both content attributes expose a read-only `System.Type[] ComponentTypes` containing
the constructor argument, a writable `System.String[] HtmlElements` defaulting to an
empty array, and a writable `System.Boolean AllowText` defaulting to `false`.
`ForwardsContentAttribute` exposes read-only `System.String[] ParameterNames` containing
its constructor argument. Arrays must be non-null, with non-null entries.

Component arrays may be empty (HTML-only or empty output), or contain classes
implementing `Microsoft.AspNetCore.Components.IComponent`, including abstract base
classes, closed constructed generics and unbound generic definitions such as `Row<>`.
Open definitions describe all constructions; closed types describe that construction.
Allowed component types include derived components and form terminal boundaries.
Partially specified generic constructions and non-component types are invalid.

HTML names use the grammar `[A-Za-z][A-Za-z0-9._-]*`, without markup, whitespace,
namespace prefixes or attributes. Comparison is ordinal and case-insensitive;
custom element names such as `my-element` are permitted. These are container names:
their descendants remain subject to the content contract. Visible text is opt-in;
formatting whitespace and comments do not count as visible text. Empty output is
always acceptable, including when every permission is empty or false. An empty
`ProducesContent` declaration means no fixed local output, not unknown output.

Forwarding names are case-sensitive property names, must be unique and non-empty,
and must resolve to public instance `[Parameter]` fragment properties (including
typed fragments). Inherited properties are supported; a hidden member takes precedence
and must itself meet these requirements. At least one name is required. Forwarding
describes the union of these parameters' outputs; combine it with `ProducesContent`
to add fixed output. It is a declaration, not runtime rendering or enforcement.

Inherited contracts remain in force. Overrides and same-named properties may repeat
an equivalent declaration; changing its permissions is a conflict. Output and forwarding
redeclarations on derived classes and output contracts on overridden methods follow
the same rule. Component/name array order and duplicates do not change the meaning
of a content permission set; HTML casing and omitted versus explicit defaults do not
create conflicts. Forwarding names must still be unique. This conservative version
requires equivalent redeclarations rather than attempting to prove subset relationships.

`BCC002` reports malformed declarations at their attribute, or at the derived class
when it breaks inherited forwarding. `BCC003` reports conflicting inherited contracts.
Both are errors tracked directly in `AnalyzerReleases.Shipped.md` for 1.0.0.

```csharp
[ProducesContent(HtmlElements = new[] { "div" }), ForwardsContent(nameof(ChildContent))]
public class Panel : ComponentBase
{
    [Parameter, AllowedChildren(typeof(Row<>), HtmlElements = new[] { "span" }, AllowText = true)]
    public RenderFragment? ChildContent { get; set; }
}
```

Declarations and local output are validated. Allowed components match exact and derived types and open/closed generic families; inherited parameter contracts remain in force. Allowed components are terminal boundaries. Forbidden HTML/text output reports BCC005; unavailable, unsupported or recursive wrapper output reports BCC006. HTML containers are checked recursively within each static slot, including HTML-only
contracts. HTML, custom elements and SVG local names all use ordinal case-insensitive
matching (for example `linearGradient` matches `lineargradient`); names with namespace
prefixes are unsupported. Text permission applies inside containers too. Razor comments,
HTML comments and whitespace are ignored; decoded visible entities count as text.
`BCC005` rejects HTML/text; `BCC004` warns for opaque `MarkupString` and unresolved
non-string output other than fragments, even when text is allowed. String expressions follow text permission.
`BCC006` warns for opaque fragment values or method results, runtime `DynamicComponent`
selection, dynamic builder component types or element names, and unsupported manual
builder helper statements. Method calls used in control-flow conditions or collection
expressions do not themselves emit content. Each restricted slot usage receives at most
one BCC006 warning; known invalid siblings and branches still produce errors. Standard
global analyzer severity settings can promote or disable these compile-time diagnostics;
they imply no runtime enforcement.
Implicit/named slots and typed template bodies receive their parameter's contract.
All conditional/switch branches and loop bodies are inspected regardless of runtime data.
Block-bodied inline fragment lambdas and local fragment initializers are expanded at emission sites; arbitrary
assignment/alias tracking remains later work. Inspectable local component render methods
are expanded recursively, binding every emitted fragment parameter to its consumer input.
Unused templates and templates passed beneath an allowed terminal component do not
contribute local output. All possible output branches are checked. Proven-empty local
components are accepted; unavailable or unsupported implementations produce BCC006,
including opaque fragment factories. Constructors and lifecycle methods are never executed.
Wrapper findings point to the consumer usage and include the wrapper/template path.
Recursive or excessively deep wrapper expansion conservatively reports BCC006;
specialised limit diagnostics remain future work.
Expression-bodied builder lambdas in slot attributes are not inspected by this slice. Non-emitting lambdas, reference captures, keys and events add no output findings.
Constant text expressions are checked without
executing code. Explicit compiled ProducesContent declarations are consumed at call
sites; inferred summaries use the separate compiled summary schema below.
Future schema changes must retain these identities/defaults or document the required
breaking version change. No runtime or Roslyn dependency is added to annotations.

Forwarded AllowedChildren parameters are checked as permission subsets at restricted
slots. BCC009 rejects any source component family, HTML name or text permission the
receiver does not permit. Typed invocation preserves the parameter contract. Base
parameter contracts remain effective even after conflicting redeclarations. Callers
are checked at the annotated boundary; opaque unannotated values remain BCC006.

## Declared factory and component output

`ProducesContent` is checked on available factory and component render bodies.
`BCC010` is an error for known output outside the declaration. `BCC011` warns once
at the factory method or component declaration when output is opaque. Callers use the declaration
and do not repeat that implementation warning. Plain and typed fragment factory
returns are supported. All inspectable return branches are checked without running
application code. Unsupported available implementation forms remain uncertain.
Factory declarations without bodies, such as abstract signatures, remain promises;
available overrides are checked against their inherited declarations.
For components, `ProducesContent` describes fixed output. `ForwardsContent` adds the
listed slots' output to that fixed output; those slots need not fit the fixed-output
permissions, but both contributions must fit the receiving slot's contract.

Use a standard C# `#pragma warning disable BCC011` around the implementation to
acknowledge uncertainty locally, then restore it. This never suppresses BCC010.
The Razor `bcc-suppress-next` convention still applies only to BCC004/BCC006 usage
warnings; it does not acknowledge a factory declaration.

Explicit declarations in referenced assemblies are trusted when compatible with
the receiving slot. They are promises by the library author, not proof of the
unavailable implementation and not a runtime guarantee. A library built without
the analyzer can publish an inaccurate declaration. Compile libraries with the
analyzer to check their available source. This does not infer or publish new
compiled output summaries by itself; inferred summaries use the publication mechanism below.

## Compiled summary schema v1

The analyzer package includes an incremental source generator. It emits assembly-level
`CompiledContentAttribute(System.Type[] componentTypes, System.Type target)` and,
when needed, `CompiledForwardingAttribute(System.String[] parameterNames, System.Type target)`.
Both types are sealed annotation attributes, permit multiple assembly declarations,
and expose read-only constructor properties plus a writable `Version` (default `1`).
`CompiledContent` also exposes `HtmlElements` (empty by default) and `AllowText` (false).
The annotation schema remains v1; summary versions are independently checked.
These attributes are build output; continue authoring promises with `ProducesContent`
and `ForwardsContent`.

Summaries infer a bounded subset of C# render methods and Razor-generated C#:
static `OpenComponent<T>`, constant element names/text, and directly emitted
fragment parameter properties, including typed invocations. Fixed component
types and forwarded names form a union. Generic wrapper targets use their open
definition; forwarded arguments are checked separately at every usage. Fixed
output containing a type parameter is unsupported. Explicit declarations take
precedence, including inherited declarations. Missing templates contribute empty output.
Only automatic fragment parameter properties without defaults or source writes
are inferred as forwarding. Custom accessors, defaults and lifecycle/event writes
remain unknown; callers cannot accidentally erase a library's default output.

The generator skips unsupported bodies as a whole, including helper calls,
dynamic output, opaque markup, assignments, object creation and throws. It does
not execute application code or inspect IL. An inferred component type is a
fixed permission boundary: it must itself be allowed by the receiver. Recursive
inference through other unallowed components is not currently published.
Use explicit output contracts for these cases.

Razor source generators cannot see one another's output. The package's direct
build targets therefore retain generated C# and perform a second compiler pass
to publish Razor summaries into the same assembly and reference assembly. This
adds compiler work to Razor builds. Design-time builds skip this publication
pass. Set `BlazorContentContractsPublishSummaries=false` to disable generation;
existing explicit annotations still survive compilation. Library authors must
reference the analyzer package directly; automatic transitive activation is a
separate delivery item. Existing generated-source output paths are preserved.

Consumers read annotations and summaries through Roslyn assembly symbols for
both project and NuGet references. Summaries must have a unique matching target,
supported version, non-null output arrays and supported component/fragment types.
Absent, duplicated or unsupported summaries remain unknown (`BCC006`), with a
rebuild/declaration suggestion. `BCC014` additionally explains unsupported summary
versions and annotation identity/schema conflicts. The annotation assembly must
have the canonical assembly name and exactly one supported schema marker.
Explicit declarations remain library-author promises; inferred summaries are
compiler-generated descriptions, not runtime enforcement or a security boundary.
