# Stable publication

`publish.yml` handles `release.published`, explicit manual tag dispatch and local
reusable calls. A published, non-preview GitHub release must already exist. Its
name must equal its `vMAJOR.MINOR.PATCH` tag or the version without `v`, and notes
must be nonempty. Tags must resolve to commits on main. Preview tags, leading
zeroes, mismatched names and versions before 1.0.0 fail closed.

Publication is disabled until the separate publishing-access task configures
repository variable `NUGET_PUBLISH_ENABLED=true`. Manual and reusable calls default
to `dry-run: true`: they resolve, build, test, pack and upload evidence, but never
request a NuGet token or push. Release events publish only when the enable guard is
set. This change does not create releases or activate a schedule.

## Trusted identity

Configure NuGet.org Trusted Publishing for owner `PinguApps`, repository
`BlazorContentContracts`, workflow file `publish.yml`, and environment `nuget`.
Restrict package ownership/policy to `PinguApps.BlazorContentContracts` and
`PinguApps.BlazorContentContracts.Analyzers`. Set GitHub Actions configuration variable `NUGET_USER` in the `nuget` environment
to the NuGet account with permission to publish both IDs. Confirm the configured
account and policy during PIN-1275; these are required setup, not evidence of an
existing policy. See [NuGet's setup instructions](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

The publisher uses only `NuGet/login`'s short-lived output credential. Stored
long-lived API keys and fallback credentials are prohibited. Only the publishing
job receives `id-token: write`. Callers must grant `contents: read` and
`id-token: write`; the publisher's reusable tests retain only `contents: read`.
Use `uses: ./.github/workflows/publish.yml` with `tag` and explicit `dry-run: false`
after guarded draft publication; do not rely on GITHUB_TOKEN release events.
Keep the caller in this repository so the configured identity remains exact.

## Artifacts, retries and compatibility

The tag resolves once to a commit SHA; reusable verification checks out that SHA.
`BCC_PACKAGE_VERSION` selects the version for the build and single release pack.
The existing tests exercise the actual packages on .NET 10 and 11 and verify
transitive activation, package layout, SourceLink, symbols and blank Unshipped
tracking. A separate next-patch analyzer package is a resolution-test fixture;
it is excluded from the release artifact. No release package is repacked.

The manifest and hashes share the validation job's trust boundary; they detect file changes, not a compromised build job. GitHub's immutable same-run artifact provides the handoff boundary.

The immutable workflow artifact contains exactly the two nupkg files, their two
snupkg files, and a manifest recording repository, source SHA, version and SHA-256
hashes. The publisher downloads these files, verifies identity and hashes, and
pushes them without building. Archive the artifact and run evidence for release
qualification. GitHub run artifacts expire according to repository retention.

Per-tag concurrency serializes publication and does not cancel an active push.
`--skip-duplicate` permits recovery after a partial push; NuGet versions are
immutable. Prefer rerunning failed jobs in the original run so validated artifacts
remain identical. Never move a released tag or replace a published version. A full
new run rebuilds and revalidates from the same source; investigate differences
before retrying a partially published release. Fix bad published content with a
new version, never a credential fallback.

Preserve the [support matrix](../README.md#initial-compatibility-targets) and
[SemVer policy](adoption.md#maintaining-and-upgrading). Breaking public API,
diagnostic or schema behaviour requires a major version and explanatory notes;
compatible additions use minor, fixes use patch. Maintainers review the release
diff and draft labels before publication: nonempty notes alone cannot establish
correct SemVer. New diagnostics go directly into `AnalyzerReleases.Shipped.md`
under their release; `AnalyzerReleases.Unshipped.md` remains the blank template.
Final artifact qualification and first live OIDC/public-feed verification belong
to PIN-1274/PIN-1275/PIN-1276. A dry run proves neither account access nor publishing.

Release-script test scenarios require PowerShell 7 (`pwsh`) alongside the SDK.
