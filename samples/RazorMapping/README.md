# Razor build mapping proof

Run `dotnet run --file scripts/verify.cs` from the repository root first. This
packs the exact local artifacts and runs Gherkin scenarios in independent Razor
projects, using isolated NuGet caches. Invalid and commented invalid usage fail
with one BCC001 error at the original `Bad` name (line 2, columns 6–9 for the
plain invalid case). Valid and empty slots pass. SARIF verifies both endpoints.
The existing package-layout and runtime-dependency checks still run.

For a sample you can inspect, restore `samples/RazorMapping/RazorMapping.csproj`
with `--source artifacts/packages --packages artifacts/sample-packages`, then
build it with `--no-restore`. It intentionally fails with BCC001. Replace `Bad`
with `Good`, or replace the usage with `<Slot />`, to see successful builds.
Use a shell with `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0` for these commands.

## Analysis and mapping

The analyzer opts into generated code and resolves Razor-generated
`RenderTreeBuilder.OpenComponent<T>` and `AddAttribute` calls by compiler symbol.
A restricted fragment parameter uses `[AllowedChildren(typeof(Good))]`.
BCC001 is an error for a definite direct child of another component type.
The current slice matches exact/base types, open and closed generic families, inherited contracts, imported and qualified tags, and inferred generic component helpers.

Razor's generated C# supplies a `#pragma checksum` identifying the original file,
but static component tags have no individual `#line` mapping in SDK 11.0.100.
Consumers must explicitly include Razor sources as `AdditionalFiles` (see sample).
The analyzer reads those snapshots through Roslyn, with cancellation, rather than
reading files from disk. That original text also retains Razor comments for later
local suppression work; this PR ignores comments but implements no suppression.

For static markup the mapper pairs component opening tags with semantic generated
calls in source order, checks counts and names, and creates an external-file
location over the original tag name. Comments are masked without shifting offsets.
A mismatch or Razor transition outside comments prevents mapping and reporting.
This is a deliberately bounded foundation, not a complete Razor parser.
Explicit named templates, directives other than using, expressions, wrapper output, HTML/text rules and dynamic fragments are not qualified
by this slice. They must not be interpreted as validated because a build passes.
Automatic transitive activation and automatic source inclusion remain later work.

## Support evidence

Build qualification: .NET 10, SDK 11.0.100 / bundled Razor and Roslyn 5.9.
The regression consumers copy `global.json` and use the real SDK generator and
compiler; `scripts/verify.cs` restores the just-packed nupkg files, not a published
feed or a shared package cache. No mocked Razor syntax trees establish this proof.

By Pingu's explicit PIN-1249 scope decision, this PR proves CLI diagnostics only.
Visual Studio and VS Code live diagnostics remain unclaimed pending PIN-1270,
which must qualify exact editor/extension/compiler versions and source mapping.
An external-file CLI location does not establish editor squiggles or code actions.
