# Analysis performance and invalidation

Run the repeatable SDK measurements from the repository root with the SDK in
`global.json` and `DOTNET_HOST_PATH` pointing to its absolute executable:

```powershell
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
& $env:DOTNET_HOST_PATH test -c Release --filter FullyQualifiedName~AnalysisInvalidation --logger 'console;verbosity=detailed'
```

The deep fixture has 70 generated level components, an Inner and Wrapper, and an
independent invalid HTML sibling. The wide fixture has 4,200 Leaf usages inside
Inner, each emitting Good, plus Wrapper and the invalid sibling. Both exceed
the supported traversal bounds: 64 active entries and 4,096 work entries per
receiving contract (forked traversals share the work budget). They must report
BCC013 and retain BCC005 for the sibling. Each SDK process has a 90-second safety
timeout; this is a test bound, not a latency guarantee for arbitrary projects.

Each measurement alternates enabled/disabled analysis twice, appending a Razor
comment to force recompilation. Restored dependencies remain cached. Disabled
uses `RunAnalyzers=false`, disabling all analyzers, so the comparison includes
other SDK analyzers and cannot isolate the cost of this package. Enabled builds
intentionally fail on the invalid sibling. Results include restore, Razor
generation, compilation and process startup. These are wall-clock observations,
not a microbenchmark or runner-cost claim. The first sample includes cold fixture
setup effects; compare later samples cautiously and repeat on an idle machine.

The same feature checks valid → invalid → valid edits without cleaning outputs
for wrapper output, receiving contracts, fragment output and a separately built
referenced library's declared output. It checks traversal cancellation, including
a fork sharing the cancelled token. Existing BoundedAnalysis, FragmentAssignments
and UnknownOutput scenarios protect contextual generic/template results and mixed
invalid/unknown output.

Render-body and fragment-source dictionaries are created inside each receiving
contract analysis. They retain syntax discovery only within that analysis; no
static cache survives compilation edits. Template inputs and receiving contracts
are validated in their own contexts. No application component is instantiated or
application output executed. These measurements do not justify extending cache
lifetime or increasing traversal bounds.

## Recorded baseline (13 November 2026)

Windows 11 build 26200, AMD Ryzen Threadripper 3970X (32 cores, 64 logical
processors), SDK 11.0.100, runtime 11.0.0, net11.0 consumers. Release packages
were packed locally and restored into each independent consumer's private package
directory. Samples below are milliseconds in execution order:

| Fixture | Enabled 1 | Disabled 1 | Enabled 2 | Disabled 2 |
| --- | ---: | ---: | ---: | ---: |
| Deep (70 levels) | 5545 | 8993 | 5796 | 9071 |
| Wide (4,200 leaves) | 14768 | 11628 | 13911 | 10779 |

These noisy single-machine samples are a repeatable baseline, not evidence of a
speed improvement. In particular, the disabled deep builds were slower. No
production cache or traversal optimisation is justified from these samples.
