# Framework behaviour catalogue

`FrameworkBehaviourCatalogue` selects internal output descriptions by resolved
Roslyn type definition, declaring assembly name, Microsoft public-key token and
assembly version. Namespace or short-name matching alone grants nothing. Generic
constructions use their original definition. Derived types deliberately receive
no inherited description: overrides, lifecycle changes and fragment accessors
can change output even without a new render method.

Supported dependency assembly versions are exactly **10.0.0.0** and **11.0.0.0**.
These are assembly identities, not NuGet patch numbers. Framework servicing patches
retain those identities; a new major/minor assembly identity requires qualification.
This registry is separate from the README's SDK, Razor and Roslyn host matrix.
Changing an SDK does not qualify another framework dependency automatically.

Descriptions preserve the external-description model's union of forwarded slots
and fixed/default output. They additionally distinguish alternatives, empty local
output, relocated output and parameter-dependent output. Slot lists are possible
output sources, not a promise that all slots render together. Default elements
are possible output, not unconditional output. Relocated content belongs elsewhere
and must not be checked against the local receiving slot. Entries without a complete
local-output model remain unresolved. Their slots/defaults are partial facts only;
they cannot establish a complete output contract or grant an exemption.

`FrameworkOutputDescription` has three fields: `Kind` classifies output, `Slots`
names possible fragment parameters, and `DefaultElements` lists possible default
HTML element names. The allowed kind values are:

| Kind | Meaning |
| --- | --- |
| `forwarding` | Output comes from the listed fragment slots. |
| `alternatives` | Alternative fragment branches can supply output. |
| `defaults` | Omitted templates can introduce the listed default elements. |
| `empty` | No local output. |
| `relocated` | Content renders outside the local receiving slot. |
| `parameter-dependent` | Parameters influence output; a family-specific evaluator is required. |
| `unresolved` | The description is incomplete; keep local output unknown. |

This schema does not encode text, parameter values, branch conditions or complete
rendering logic. Those belong to the family-specific evaluators and their tests
in PIN-1264–1268, rather than being inferred from slot/default lists.

PIN-1263 supplies selection and metadata. PIN-1265–1268 implement each remaining family's
parameter/default/template evaluation and enable its use in content validation.
The four `forwarding` providers are active on both supported identities: their supplied
`ChildContent` is checked against the receiving contract without adding HTML output.
`FormMappingScope` has a typed `RenderFragment<FormMappingContext>` slot; the other
three providers have plain `RenderFragment` slots. Nested providers retain the same
receiving contract. AuthorizeView alternatives are also active: every supplied
`Authorized` (or its
alternative `ChildContent`), `NotAuthorized` and `Authorizing` fragment is checked,
regardless of the current authentication state. Omitted fragments emit nothing.
`Authorized` and `ChildContent` must not both be supplied: the framework rejects
that combination at runtime, even though Razor can compile it. This analyzer checks
output contracts and does not validate framework parameter combinations. Other
description kinds grant no transparency yet, except ErrorBoundary defaults: both supplied
`ChildContent` and typed `ErrorContent` are checked; omitted `ErrorContent` adds the
framework default empty `div`. An explicitly empty error template replaces that default.
Allowed HTML still has its descendants checked; explicitly allowed ErrorBoundary
components remain terminal boundaries. Explicit
external descriptions and author contracts keep their existing precedence.
Unsupported identities and derived framework implementations produce actionable
BCC006 uncertainty when no inspectable source or explicit contract is available.
Known invalid sibling output remains an error. No new diagnostic ID is needed.

## Qualifying a contribution

1. Resolve the actual type and declaring assembly using the supported framework
   reference packs; namespaces do not determine assembly ownership.
2. Inspect that version's implementation. Add its metadata name and declaring
   assembly mapping in `FrameworkBehaviourCatalogue.Describe`. Choose one of the
   kinds above and record its fragment slots and possible default element names.
   Document additional text, parameter conditions and rendering facts alongside
   the family-specific evaluator/tests before activating its rendering rule.
   Add a separate version branch when behaviour differs; never expand a version
   range based only on matching API names.
3. Add an independent row to `FrameworkCatalogue.feature`. Assert slot names against
   actual symbols, plus generic constructions, impersonation and derived behaviour
   where relevant. Synthetic identity tests exercise unsupported versions without
   claiming integration support for those versions.
4. For rendering-rule activation, add real Razor consumers using both supported
   framework dependencies and the packed analyzer. Exercise defaults, supplied
   templates, invalid siblings and uncertainty. Metadata selection alone does not
   prove rendering behaviour. Run `dotnet run --file scripts/verify.cs` and retain
   actual results; do not change the blank Unshipped release template.

The registry tests currently exercise installed .NET 10 and .NET 11 framework symbols and
synthetic .NET 10/11 selection identities. Real packed Razor consumers exercise
the conservative derived-component path on both .NET 10 and .NET 11.
