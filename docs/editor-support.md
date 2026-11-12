# Razor editor qualification

Live diagnostics are qualified for the Windows hosts below with a .NET 10
consumer. This does not qualify Rider, other editor versions, preview SDKs or
.NET 11 live editing. The separate CLI matrix remains in the root README.

## Observed hosts (12 November 2026)

| Host | Versions | Diagnostics | Suppression insertion |
| --- | --- | --- | --- |
| Visual Studio Community | 18.11.12306.390; bundled compiler 5.11.0-1.26424.5; bundled Razor 11.1.0-preview.26424.5 | Original Razor squiggles and IntelliSense error-list entries | Not offered by the default Razor action menu; insert the documented comment manually |
| VS Code | 1.139.0, commit 2242ebbb54efeeb0129e08e919e7e8d43033cd83; C# 11.9.40; C# Dev Kit 11.9.39; language-server Roslyn 5.14.0-1.26554.1; Razor 11.2.0-preview.26554.1 | Original Razor squiggles and Problems entries | Qualified with the opt-in setting below; the host labels the action “Untested” |

Both hosts used .NET SDK **10.0.402**, targeting `net10.0`. That SDK's compiler
reports `5.9.0-1.26505.111` and its bundled Razor compiler reports
`10.4.0-preview.26505.111`. These are the binaries' informational version strings,
including their internal preview labels; the selected SDK itself is not a preview.
The analyzer and fixer retain Roslyn **5.9.0** dependencies and target
`netstandard2.0`. The fixer is a separate assembly; compiler-only analysis has no
Workspaces dependency.

VS Code initially ran C# 11.7.18, then activated an already-installed 11.9.40
update on reload. The complete fixture and action evidence above uses 11.9.40;
11.7.18 is not independently qualified by that evidence.

## Repeat the smoke check

1. Run `dotnet run --file scripts/verify.cs` to produce the local 1.0.0 packages.
   Set `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0` in the child environment. Keep the
   existing process/user/machine PATH values unchanged.
2. Run `./scripts/prepare-editor-smoke.ps1` in PowerShell 7. It prints a new,
   isolated directory containing the fixtures, local package-feed configuration
   and its own package cache. Existing destinations are refused. Use
   `-SdkVersion` to select an installed SDK for another qualification run; record
   that version rather than treating it as covered by the table above.
3. From that directory, use the absolute path of the selected SDK's `dotnet`
   executable to build `Consumer/Consumer.csproj`. This deliberately fails with
   **7 BCC errors and 3 BCC warnings**. Inspect `Consumer/diagnostics.sarif`.
4. Open the directory in VS Code, or `Consumer/Consumer.csproj` in Visual Studio.
   Open `Consumer/Usage.razor` and wait for project loading and analysis. In Visual
   Studio, select IntelliSense diagnostics in the Error List to distinguish live
   results from older build results.
5. Compare the following lines with CLI results. Empty and permitted content on
   lines 8–9 have no content-contract finding.

| Usage.razor line | Expected diagnostic and location |
| --- | --- |
| 1 | Two BCC005 errors on `Bad`: forbidden div and visible text |
| 2 | Two BCC005 errors on `LocalWrapper`, with `LocalWrapper -> Bad` in their messages |
| 3 | BCC012 on `Recursive`, including `Recursive -> Recursive` and a repair suggestion |
| 4 | BCC006 on the referenced library method expression |
| 5 | BCC009 on the receiving `Slot`, naming `EditorContracts.Wrappers.CompiledWrapper` |
| 6 | Two BCC005 errors on the generic `GenericWrapper` identifier |
| 7 | BCC004 on the opaque `MarkupString` expression |

The consumer references Wrappers, which references Library, which references the
packed annotations/analyzer packages. The consumer has no direct analyzer or
annotation reference. Library and Wrappers are built as actual assemblies before
the consumer; no application code is executed to obtain their output descriptions.

For SDK discovery problems, check the editor's selected SDK root as well as
`global.json`. A successful CLI command using another installation does not prove
the editor can find that SDK. During this qualification, Visual Studio was
launched with process-local `DOTNET_ROOT` and
`DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` pointing to the existing system installation.
No persistent environment settings or PATH values were changed.

## Acknowledge one reviewed usage

In VS Code's workspace settings, enable:

```json
{ "razor.advanced.showAllCSharpCodeActions": true }
```

On BCC004 or BCC006, open Quick Fix and choose **Acknowledge reviewed output with a
Razor comment (Untested)**. Review the output before acknowledging it. The action
inserts `@* bcc-suppress-next BCC006: reviewed output *@` (or BCC004) directly
before that component/expression. Replace `reviewed output` with a useful project
reason. Save the Razor file to refresh the additional-source snapshot and its
suppression diagnostics.

The insertion adds only a Razor comment: it adds no whitespace, HTML or runtime
component. There is no Fix All action. Known-invalid IDs, recursion and analysis
limits are not offered this suppression. Unknown siblings and known violations
remain checked, including when unknown warnings are promoted to errors.

The default Visual Studio menu did not offer this action on BCC006; its observed
menu offered component extraction instead. Visual Studio diagnostics and manually
inserted suppression comments are qualified, but automatic insertion is not
claimed there. The VS Code opt-in is a host setting, not a package installation
requirement for diagnostics.

## Edit and invalidation evidence

The actual fixture initially produced the same 7 errors/3 warnings in CLI,
VS Code and Visual Studio. Saved edits were observed to remove their stale
findings while preserving independent findings:

- Replacing the first `Bad` usage with `Good` removed its two errors.
- Changing local wrapper output to `Good` removed the two wrapper errors.
- Breaking the recursive wrapper removed BCC012.
- Rebuilding Wrappers with an empty declared output removed BCC009 in the consumer.
- The VS Code action inserted BCC006 and BCC004 comments into original Razor files;
  saving removed those warnings while known-invalid content remained diagnosed.
- After repairing the remaining generic wrapper, the CLI consumer built with zero
  warnings/errors, VS Code showed “No problems” and Visual Studio showed “No issues
  found”. Restoring the fixture restored the original 7 errors/3 warnings.

The smoke cache's consumed analyzer nupkg SHA-256 was
`A53A491B8CF07FEC697C78CDB57236A7A870B87CDCD35AD76C01D9D1197C2A90`;
the annotations nupkg was
`A16027390780261BF3C7A86DF97AF5024657FE03ED876D88B9F5134FDE0204C4`.
These identify this local smoke run, not published release artifacts.

Unsaved Razor and AdditionalFiles snapshots can differ. Suppression refresh is
qualified after saving; unsaved comment changes are not claimed to update
immediately. Referenced compiled summaries describe the last normal library build:
rebuild an edited library before expecting its new output contract in a consumer.
The package does not inspect arbitrary library IL.

## Mapping and regression checks

CLI diagnostics retain external original-Razor locations. Design-time builds add
a separate `obj/.../bcc-editor.globalconfig` with a package-specific editor marker.
Normal builds neither include nor overwrite that configuration, so a CLI rebuild
cannot switch off editor mapping through the shared generated editorconfig.
During editor analysis the analyzer reports on generated
component identifiers or mapped expressions so Razor's internal source mappings
can translate them back to the original file. Original location data travels with
the diagnostic for the fixer to edit the Razor additional document. Namespace
prefixes and generic type arguments are excluded from a component's generated
diagnostic span.

The executable `EditorDiagnostics.feature` scenarios check real SDK design-time
diagnostic locations, a following normal build with the editor marker preserved,
and apply the fixer through Roslyn before building the changed
Razor consumer. The fixer test supplies a diagnostic directly; it proves edit and
suppression semantics, not editor routing. The live host checks above establish
routing. The normal suppression suite separately checks rendering equivalence,
malformed/unused comments and severity configuration on .NET 10 and .NET 11.

Missing or ambiguous source mappings retain the conservative CLI fallback; this
does not promise an editor squiggle for every unsupported Razor syntax form.
