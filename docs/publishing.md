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
set. The draft-publication channel below has a separate activation guard.

## Guarded weekly drafts

`publish-release-draft.yml` supports manual dispatch (default `dry-run: true`)
and Sunday 10:17 UTC (`17 10 * * 0`). Scheduled runs do nothing until the owner
sets `WEEKLY_PUBLISH_ENABLED=true`. Both that variable and
`NUGET_PUBLISH_ENABLED=true` are required to publish a draft, including manually.
Do not set them as part of this implementation. Owner activation follows the
first stable release, live-feed verification and a refreshed schedule audit.

The selector requires an existing stable release on main, new commits on main,
and exactly one stable draft with a strictly greater stable version. Dependency,
documentation and metadata commits all qualify; no paths or note-content
heuristics decide whether changes deserve a release. Notes must still be nonempty
to satisfy the reusable publisher's release contract. The greatest published
stable version is the baseline; prereleases never establish the first stable.
Invalid stable identities, including missing or mismatched draft names, fail closed.
A draft must target main or its current
SHA; an existing draft tag must equal current main. Stale or off-main tags are
rejected rather than moved. Missing baseline, commits or draft skips publication.

Manual and scheduled work share a non-cancelling concurrency group. Publication
pins the draft target to the inspected main SHA, then explicitly calls the local
reusable `publish.yml` with `dry-run: false` and scoped OIDC permissions.
GITHUB_TOKEN does not trigger a second release workflow run. The reusable
publisher retains its per-tag lock and duplicate-safe immutable NuGet pushes.
If draft publication succeeds but package publication fails, rerun failed jobs
in that run or dispatch `publish.yml` with `tag=<already-published-tag>` and
`dry-run=false`, with `NUGET_PUBLISH_ENABLED=true`. A new weekly
run intentionally does not republish it. Fix content using a new version.

The 2026-11-14 read-only audit of PinguApps repositories found draft-publishing
Sunday UTC slots: Dashboard.Railway 08:02, RabbitMQ.Railway 08:17,
Railway.Jobs 08:32, Railway 08:47, RazorStyle 09:02, Upstash.Redis 09:17,
PostgreSQL.Railway and UpdateAssistant 09:32, BlazorSitemap 09:39,
Bunny.Storage 09:47, CodeStyle.Analyzers 10:02, and Playbook 18:17.
10:17 is provisional and separated by 15 minutes from the latest morning slot.
This is a snapshot of matching draft-publication workflows, not a reservation;
the owner must refresh the portfolio audit before activation.

Offline Gherkin scenarios exercise `Select-ReleaseDraft.ps1` against temporary
real Git repositories without GitHub writes or NuGet access. A manual dry run
records the selected tag/SHA (or skip) in the run's job summary and never publishes a draft or calls the
package publisher. No live publication is required to verify these guards.

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
