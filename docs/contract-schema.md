# Content contract metadata schema v1

The annotation assembly is `PinguApps.BlazorContentContracts`. Its assembly metadata
key `BlazorContentContracts.SchemaVersion` has value `1`. All three public sealed
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

This slice validates declarations and preserves metadata. Checking implementation
truth, generic/derived child matching, recursive HTML/text validation and forwarding
composition are later work. The existing BCC001 proof still checks only static,
direct, exact component types; these annotations do not imply those later checks run.
Future schema changes must retain these identities/defaults or document the required
breaking version change. No runtime or Roslyn dependency is added to annotations.
