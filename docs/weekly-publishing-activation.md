# Weekly publishing activation

PIN-1283: Pingu explicitly approved Sunday **10:17 UTC** (`17 10 * * 0`)
and activation in the implementation conversation on 2026-11-15.
The repository variable `WEEKLY_PUBLISH_ENABLED=true` was set and read back
through authenticated GitHub APIs at **2026-11-15 21:57:42 UTC**.
`NUGET_PUBLISH_ENABLED=true` remains unchanged. The scheduled workflow is active.

## Prerequisite evidence

- [Stable v1.0.0](https://github.com/PinguApps/BlazorContentContracts/releases/tag/v1.0.0)
  was published from `e28393cd60bc8160ee00b6c84038abeb76373b46`.
  [Publisher run 43667899705](https://github.com/PinguApps/BlazorContentContracts/actions/runs/43667899705)
  completed validation, trusted NuGet login and package/symbol pushes successfully.
- [PIN-1276](https://linear.app/pinguapps/issue/PIN-1276) records the isolated
  public-feed-only consumer passing valid content and reporting BCC005 for invalid
  content, plus matching custom icon bytes on both package galleries. This is
  prior release evidence; activation did not rebuild or republish the packages.
- Trusted identity remains owner `PinguApps`, repository `BlazorContentContracts`,
  workflow `publish.yml`, environment `nuget`. The environment variable
  `NUGET_USER=pingu2k4` was read back before activation. The successful live OIDC
  run is the identity evidence; no stored-key fallback was added.
- At **2026-11-15 21:19:10 UTC**, all 37 organization repositories were enumerated
  again. Every default-branch workflow path and Git blob SHA matched the
  [portfolio audit](publishing-schedule-audit.md)'s pinned snapshot. The empty
  PinguApps repository was accounted for separately. All 127 workflow files
  therefore retain their audited contents and cron values. Sunday 10:17 remains
  15 minutes after CodeStyle.Analyzers and eight hours before Playbook.
- Main was `847b5a623f232f758a0f6a1897ea4e6797aef5f4` before activation; the guarded
  workflow and publisher were unchanged. Manual dispatch, no-change, no-draft,
  first-stable, version-selection and concurrency safeguards remain intact.

## Next scheduled run: monitoring handoff

The next nominal run is **Sunday 2026-11-22 10:17 UTC**. GitHub scheduling can be
delayed; this is an expected start, not a completion guarantee. Configuration
activation is complete independently of observing that future run.

Pingu should inspect the next `schedule` run of
[Publish stable release draft](https://github.com/PinguApps/BlazorContentContracts/actions/workflows/publish-release-draft.yml).
Confirm its event, selected tag/source SHA or documented skip, and result.
When an eligible draft exists, follow the reusable package publisher through
trusted login and immutable package pushes, then check the resulting NuGet
versions and public-feed consumer. A no-change/no-draft skip is a valid result.
Record the run URL, tag/SHA or skip reason, and publication/feed result on
[PIN-1283](https://linear.app/pinguapps/issue/PIN-1283).

No release was created and no live publication was dispatched to activate this
gate. The existing v1.0.1 draft remains unpublished at activation. This record
does not claim that the future scheduled run has executed.

To suspend weekly publication, set `WEEKLY_PUBLISH_ENABLED=false` in repository
Settings > Secrets and variables > Actions > Variables. Manual package
publication remains available through `publish.yml`; retain its existing guards.
For package failures after draft publication, follow the existing
[retry procedure](publishing.md#guarded-weekly-drafts), using the already published
tag rather than creating another release.
