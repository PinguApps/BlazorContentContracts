# Verification and CI budget

Run the repository's shared verification command with the SDK from `global.json`:

```powershell
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
dotnet run --file scripts/verify.cs
```

Use the absolute path to your installed dotnet executable where required by your
environment. Verification checks that process and saved Windows PATH values stay
unchanged. It packs the actual packages and retains qualification hashes, TRX
results and per-framework invocation metrics under `artifacts/`.

## Keep behavioural coverage without an SDK process per case

The detailed Razor scenarios invoke the SDK's real Razor source generator through
Roslyn's generator driver, followed by the shipped content analyzers. They keep
their existing assertions for diagnostics, source spans, wrappers, generics,
templates, mutation, branches, uncertainty and suppressions. Source fixtures are
isolated, but these cases do not restore or build a new SDK project.

The direct host rejects unmodelled project/compiler properties, conditional
properties and unsupported MSBuild arguments rather than silently ignoring them.
Such cases must use an `@Sdk` scenario. Its references come from the test host's
runtime implementation assemblies, not framework reference packs; SDK-specific
reference-surface and build behaviour remains covered by the real consumers.

Compiled-library variations emit real assemblies and consume their metadata after
the fixture deletes library source. The actual summary generator supplies the
compiled contracts. This is compiler/metadata coverage; it does not establish
NuGet or MSBuild behaviour.

Scenarios tagged `@Sdk` retain real SDK builds for package activation, representative
NuGet/project-reference libraries, custom generated paths, editor configuration,
adoption examples and original Razor locations. Edit invalidation, rendering and
the bounded SDK measurements also retain real build/rendering paths. The .NET 10
pass runs the `@Net10` integration scenarios instead of repeating the full behavioural
matrix. Framework catalogue scenarios separately exercise both supported framework
identities. The README's code blocks are compiled directly, with representative
examples also checked by both real SDK consumers.

Verification rejects an empty test selection, missing SDK work, or any in-process
compiler invocation in the .NET 10 pass. Old TRX/metrics receipts are removed before
each pass so stale evidence cannot satisfy these checks.

The test host uses the compiler matching the .NET 11 Razor SDK. This is a test-only
dependency: distributable analyzer and code-fix projects retain their qualified
Roslyn 5.9 baseline and .NET 10 compatibility is exercised with packed artifacts.

## One shared suite, no automatic merge duplicate

PR validation and deliberate release qualification both call `_run-tests.yml`.
Merging a PR no longer starts another full suite automatically. `build.yml` remains
available for an explicit manual qualification run. Release qualification still
tests its exact versioned artifacts and hands those same immutable files to the
OIDC publisher without repacking. A new qualification is appropriate when its
source/version differs from prior evidence.

The shared job has a five-minute timeout. This is a cost ceiling, not evidence
that a run passed or that removing coverage is acceptable. Investigate regressions
using TRX durations and `*.metrics.json` (compiler invocations, SDK commands and
elapsed time). Compare the complete job duration and summed runner time; adding
parallel jobs alone does not establish a saving.

Changing tests or documentation does not publish a package. Publication continues
to require an explicit release/tag workflow. Weekly publication remains separately
guarded and is not activated by this optimisation.
