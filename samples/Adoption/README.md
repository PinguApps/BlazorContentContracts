# Executable contract examples

Follow the [adoption guide](../../docs/adoption.md) to pack and build this library.
Branches covers empty content, terminal generic components, inherited contracts,
HTML/text, local forwarding and declared factory output. TypedAndAssigned covers
typed named templates, closed generic permissions and lifecycle reassignment.
Framework covers every authorization branch and explicit error/virtualization
templates. External demonstrates a consumer-owned trust declaration;
ReviewedMarkup demonstrates one-instance Razor suppression.

The companion [Gherkin suite](../../tests/PinguApps.BlazorContentContracts.Tests/Features)
contains runnable negative and default-output examples. It creates independent
consumers from these Razor examples and packed artifacts, rather than relying
on text-only snippets. `scripts/verify.cs` runs them on .NET 10 and 11.

| Behaviour to explore | Executable feature |
| --- | --- |
| Generic families, inheritance, terminal components, HTML/text and every conditional/switch/loop branch | RazorBuild |
| Every emitted/unused/typed wrapper slot | LocalWrappers |
| Annotated parameter permission subsets | ParameterForwarding |
| Factory branches and trusted compiled declarations | OutputContracts, CompiledLibraries |
| External output, forwarding, trust and precedence | ExternalDescriptions |
| Reassignment, aliases and mutation uncertainty | FragmentAssignments |
| Unknown beside invalid output | UnknownOutput |
| Recursive paths and analysis budgets | BoundedAnalysis |
| Local suppression, severity, unused/malformed feedback and rendering equivalence | RazorSuppressions, EditorDiagnostics |
| Cascading and typed form-context providers | FrameworkForwarding |
| Authorization alternatives | AuthorizeView |
| Explicit, empty and default error templates | ErrorBoundary |
| Spacer defaults, dynamic spacers, item/loading/empty output | Virtualize |
| Relocated/empty output, unknown outlets, form/token/focus output and render modes | NonlocalFrameworkOutput |
| Automatic package/project activation and opt-outs | PackageActivation |

To explore a rejection, change `<HtmlSlot><span>…</span></HtmlSlot>` to
`<HtmlSlot><span><div /></span></HtmlSlot>`. The div produces BCC005 even though
its parent span is allowed. The Adoption feature protects this behaviour and
checks the unmodified sources compile. Test scenarios intentionally expecting
errors pass only when those errors are observed; the adoption library itself
builds cleanly. These checks exercise build-time analysis, not a running UI.
