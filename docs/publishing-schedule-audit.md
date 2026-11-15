# Portfolio publishing schedule audit

Observed through 11/15/2026 16:01:53 (UTC), using authenticated GitHub REST reads.

PIN-1282: all 37 organization repositories were enumerated, including archived repositories. Default-branch Git trees and every workflow YAML blob were read; commit-pinned links below identify the inspected snapshots. PinguApps/PinguApps is empty (HTTP 409, size 0), rather than an unreadable workflow directory. No other tree was unavailable.

## Decision

Retain Sunday **10:17 UTC**, cron `17 10 * * 0`, for owner approval in PIN-1283. No other publishing cron occupies that minute. The nearest preceding publisher is CodeStyle.Analyzers at 10:02 UTC (15 minutes); the next is Playbook at 18:17 UTC (8 hours). PostgreSQL.Railway and UpdateAssistant already share 09:32; that existing overlap does not involve this slot.

This confirms separation of configured start times, not completion times: delayed or long-running GitHub jobs can overlap. The slot is not reserved. Repeat this audit if activation is delayed or portfolio workflows change.

## Scheduled workflows

All cron values below are UTC. Every scheduled workflow is listed, including maintenance jobs, to avoid relying on publication filenames alone. Archived repositories cannot run scheduled workflows. Guarded workflows are included conservatively whether or not their activation variable is enabled.

| Repository | Workflow snapshot | Cron |
|---|---|---|
| PinguApps/AppwriteSdk | [.github/workflows/stale.yml](https://github.com/PinguApps/AppwriteSdk/blob/7517a7589eb2fdfb43b2fd8b421db0fa6f2c99e1/.github/workflows/stale.yml) | `27 0,12 * * *` |
| PinguApps/Aspire.Hosting.Bunny.Storage | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/Aspire.Hosting.Bunny.Storage/blob/7af74915472d8fb194622ea5b828524ec574f144/.github/workflows/publish-release-draft.yml) | `47 9 * * 0` |
| PinguApps/Aspire.Hosting.Dashboard.Railway | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/Aspire.Hosting.Dashboard.Railway/blob/4b89813263cfec51a3f26103c45bc7635a3d8e03/.github/workflows/publish-release-draft.yml) | `2 8 * * 0` |
| PinguApps/Aspire.Hosting.PostgreSQL.Railway | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/Aspire.Hosting.PostgreSQL.Railway/blob/c1e96f6d6f54b4ca55f77576ee1f0079d087f4ab/.github/workflows/publish-release-draft.yml) | `32 9 * * 0` |
| PinguApps/Aspire.Hosting.RabbitMQ.Railway | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/Aspire.Hosting.RabbitMQ.Railway/blob/250b5affa68bf631251eb28ea0ee23b800da4d81/.github/workflows/publish-release-draft.yml) | `17 8 * * 0` |
| PinguApps/Aspire.Hosting.Railway | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/Aspire.Hosting.Railway/blob/f6e56440a59792456c721d01a9e494a312fed66c/.github/workflows/publish-release-draft.yml) | `47 8 * * 0` |
| PinguApps/Aspire.Hosting.Railway.Jobs | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/Aspire.Hosting.Railway.Jobs/blob/d1b271950262705d59f570e5560dad350cb67248/.github/workflows/publish-release-draft.yml) | `32 8 * * 0` |
| PinguApps/Aspire.Hosting.Upstash.Redis | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/Aspire.Hosting.Upstash.Redis/blob/45a75f8dc2192e1a035a1a1f8f9b0f43966d82dc/.github/workflows/publish-release-draft.yml) | `17 9 * * 0` |
| PinguApps/BlazorContentContracts | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/BlazorContentContracts/blob/4e66ead433be6ed863ac5db1bb4c84dfae60cbb0/.github/workflows/publish-release-draft.yml) | `17 10 * * 0` |
| PinguApps/BlazorSitemap | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/BlazorSitemap/blob/a03cbe3e4c9c7eb87a2d219c03deb34db940d55d/.github/workflows/publish-release-draft.yml) | `39 9 * * 0` |
| PinguApps/CodeStyle.Analyzers | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/CodeStyle.Analyzers/blob/c64329ea6d7ede9725fb5542707d89cda9de378b/.github/workflows/publish-release-draft.yml) | `02 10 * * 0` |
| PinguApps/Playbook | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/Playbook/blob/444481b18b728968217e240e94f2c645dd5ccc18/.github/workflows/publish-release-draft.yml) | `17 18 * * 0` |
| PinguApps/RazorStyle | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/RazorStyle/blob/3329abfd685d2501f6ee47a90b00dbe4734b9827/.github/workflows/publish-release-draft.yml) | `02 9 * * 0` |
| PinguApps/Template | [.github/workflows/kuma-backup.yml](https://github.com/PinguApps/Template/blob/1235a1e9fc241088a2469c8506f3cc491f966302/.github/workflows/kuma-backup.yml) | `37 2 * * *` |
| PinguApps/Template | [.github/workflows/shared-services.yml](https://github.com/PinguApps/Template/blob/1235a1e9fc241088a2469c8506f3cc491f966302/.github/workflows/shared-services.yml) | `43 6 * * 1` |
| PinguApps/Template | [.github/workflows/umami-backup-maintenance.yml](https://github.com/PinguApps/Template/blob/1235a1e9fc241088a2469c8506f3cc491f966302/.github/workflows/umami-backup-maintenance.yml) | `17 3 * * *` |
| PinguApps/UpdateAssistant | [.github/workflows/publish-release-draft.yml](https://github.com/PinguApps/UpdateAssistant/blob/f0101ab560486cecb365368f09ae7ee5e8b4c137/.github/workflows/publish-release-draft.yml) | `32 9 * * 0` |
| PinguApps/Wrevo | [.github/workflows/acr-cleanup.yml](https://github.com/PinguApps/Wrevo/blob/6e0d0a0163ffbd23ae772254af626c7f368c127a/.github/workflows/acr-cleanup.yml) | `0 6 * * *` |

## Complete repository coverage

Counts include every workflow, scheduled or event-triggered. A zero count means no workflow YAML at the inspected default branch.

| Repository | Default branch snapshot | Archived | Workflow files |
|---|---|---|---|
| PinguApps/AgentOrchestrator | [main](https://github.com/PinguApps/AgentOrchestrator/tree/962651bb8353691f6843dd722b2bce78cab69142/.github) | False | 0 |
| PinguApps/AppwriteMigrator | [dev](https://github.com/PinguApps/AppwriteMigrator/tree/f85fad8541fdecd87ad70a9513d03844c53cb931/.github) | True | 6 |
| PinguApps/AppwriteSdk | [dev](https://github.com/PinguApps/AppwriteSdk/tree/7517a7589eb2fdfb43b2fd8b421db0fa6f2c99e1/.github) | True | 8 |
| PinguApps/Artestra | [main](https://github.com/PinguApps/Artestra/tree/4e10274de1d2b1a8200ded5e8c55593d96856ac2/.github) | True | 0 |
| PinguApps/Aspire.Hosting.Bunny.Storage | [main](https://github.com/PinguApps/Aspire.Hosting.Bunny.Storage/tree/7af74915472d8fb194622ea5b828524ec574f144/.github) | False | 6 |
| PinguApps/Aspire.Hosting.Dashboard.Railway | [main](https://github.com/PinguApps/Aspire.Hosting.Dashboard.Railway/tree/4b89813263cfec51a3f26103c45bc7635a3d8e03/.github) | False | 6 |
| PinguApps/Aspire.Hosting.PostgreSQL.Railway | [main](https://github.com/PinguApps/Aspire.Hosting.PostgreSQL.Railway/tree/c1e96f6d6f54b4ca55f77576ee1f0079d087f4ab/.github) | False | 6 |
| PinguApps/Aspire.Hosting.RabbitMQ.Railway | [main](https://github.com/PinguApps/Aspire.Hosting.RabbitMQ.Railway/tree/250b5affa68bf631251eb28ea0ee23b800da4d81/.github) | False | 6 |
| PinguApps/Aspire.Hosting.Railway | [main](https://github.com/PinguApps/Aspire.Hosting.Railway/tree/f6e56440a59792456c721d01a9e494a312fed66c/.github) | False | 6 |
| PinguApps/Aspire.Hosting.Railway.Jobs | [main](https://github.com/PinguApps/Aspire.Hosting.Railway.Jobs/tree/d1b271950262705d59f570e5560dad350cb67248/.github) | False | 6 |
| PinguApps/Aspire.Hosting.Upstash.Redis | [main](https://github.com/PinguApps/Aspire.Hosting.Upstash.Redis/tree/45a75f8dc2192e1a035a1a1f8f9b0f43966d82dc/.github) | False | 6 |
| PinguApps/Blazor.QRCode | [main](https://github.com/PinguApps/Blazor.QRCode/tree/61733c09711e013caf732f928e225fe2f2bacab9/.github) | True | 4 |
| PinguApps/BlazorContentContracts | [main](https://github.com/PinguApps/BlazorContentContracts/tree/4e66ead433be6ed863ac5db1bb4c84dfae60cbb0/.github) | False | 7 |
| PinguApps/BlazorSitemap | [main](https://github.com/PinguApps/BlazorSitemap/tree/a03cbe3e4c9c7eb87a2d219c03deb34db940d55d/.github) | False | 6 |
| PinguApps/BrighterMerchant | [main](https://github.com/PinguApps/BrighterMerchant/tree/0922496cec528a230ed1869818dd31c42cf31c38/.github) | False | 0 |
| PinguApps/CodeStyle.Analyzers | [main](https://github.com/PinguApps/CodeStyle.Analyzers/tree/c64329ea6d7ede9725fb5542707d89cda9de378b/.github) | False | 6 |
| PinguApps/ForgeHaven | [main](https://github.com/PinguApps/ForgeHaven/tree/519ce1e4b5c9e72b8439bbbfe4ea4e495c73c3e1/.github) | False | 2 |
| PinguApps/PackageHold | [main](https://github.com/PinguApps/PackageHold/tree/70262a0f07425c3219123fe2ebe12ffc3494f886/.github) | True | 0 |
| PinguApps/PinguApps | Empty repository | False | 0 |
| PinguApps/Playbook | [main](https://github.com/PinguApps/Playbook/tree/444481b18b728968217e240e94f2c645dd5ccc18/.github) | False | 6 |
| PinguApps/PopSalt | [main](https://github.com/PinguApps/PopSalt/tree/d345d5050f5844ebf12bac1a3ef09b1c8ba53e0e/.github) | True | 0 |
| PinguApps/PRAgent | [main](https://github.com/PinguApps/PRAgent/tree/f9f0f3ad93e38fa7c41c5909404b97002a63b5df/.github) | False | 1 |
| PinguApps/PRInspector | [main](https://github.com/PinguApps/PRInspector/tree/6ad30c3352cc7942c7acb6232c9eda6514bbed8a/.github) | False | 1 |
| PinguApps/RazorStyle | [main](https://github.com/PinguApps/RazorStyle/tree/3329abfd685d2501f6ee47a90b00dbe4734b9827/.github) | False | 6 |
| PinguApps/renovate-config | [main](https://github.com/PinguApps/renovate-config/tree/aa063d5d0e3b55742c33c0ad442c26fbd06328cd/.github) | False | 1 |
| PinguApps/SaasTemplate | [dev](https://github.com/PinguApps/SaasTemplate/tree/33d57f74bdd277aa838e3e06e61642c38e9ca239/.github) | True | 5 |
| PinguApps/scroll-world | [main](https://github.com/PinguApps/scroll-world/tree/ee464ff91dd9e35e95046b4c099022f46e34763c/.github) | False | 0 |
| PinguApps/ShadcnPlayground | [main](https://github.com/PinguApps/ShadcnPlayground/tree/be7029c9a8f875add34f930a4179fb221071b448/.github) | True | 0 |
| PinguApps/Skills | [main](https://github.com/PinguApps/Skills/tree/69b94c68cbb5f43f984cd2ec9bb7d3e672ba646b/.github) | False | 0 |
| PinguApps/Slate | [main](https://github.com/PinguApps/Slate/tree/3bd07d1b2f5c62016967bacee61aee22d0615d45/.github) | False | 0 |
| PinguApps/SlatePlayground | [main](https://github.com/PinguApps/SlatePlayground/tree/bb547de12147132e23cbc191f66618fe285489f0/.github) | True | 0 |
| PinguApps/SupabaseTest | [main](https://github.com/PinguApps/SupabaseTest/tree/eac4386e2e717593a806e580d8d5a6c4cfe2a60e/.github) | True | 0 |
| PinguApps/Template | [main](https://github.com/PinguApps/Template/tree/1235a1e9fc241088a2469c8506f3cc491f966302/.github) | False | 5 |
| PinguApps/Template.PIN646.Integration | [main](https://github.com/PinguApps/Template.PIN646.Integration/tree/0d97ffe7a0c1377a27efa8bacd2d71043fbe2447/.github) | False | 5 |
| PinguApps/UpdateAssistant | [main](https://github.com/PinguApps/UpdateAssistant/tree/f0101ab560486cecb365368f09ae7ee5e8b4c137/.github) | False | 6 |
| PinguApps/UptimeBridge | [main](https://github.com/PinguApps/UptimeBridge/tree/d6d2e422faa049b686afb92841c267127cc7dfb8/.github) | False | 4 |
| PinguApps/Wrevo | [dev](https://github.com/PinguApps/Wrevo/tree/6e0d0a0163ffbd23ae772254af626c7f368c127a/.github) | True | 6 |

## Activation handoff (PIN-1283; owner action)

1. Confirm PIN-1276 public-feed consumer and gallery evidence is complete. GitHub v1.0.0 exists (published 2026-11-15T02:27:47Z) and its publisher run succeeded; that alone does not prove all PIN-1276 criteria.
2. Approve Sunday 10:17 UTC explicitly. If a refreshed audit finds a clash, obtain owner approval for a replacement before changing cron.
3. Verify `.github/workflows/publish-release-draft.yml` on main still uses `17 10 * * 0`, guarded by `WEEKLY_PUBLISH_ENABLED`, with both publishing variables required for live publication.
4. In repository Settings > Secrets and variables > Actions > Variables, set `WEEKLY_PUBLISH_ENABLED=true` only after the preceding checks and owner approval. Keep the existing `NUGET_PUBLISH_ENABLED=true` and trusted publisher identity.
5. Follow [publishing guidance](publishing.md) for a manual dry run, selected tag/SHA evidence, and first live weekly-run verification. To suspend weekly publication, remove or set `WEEKLY_PUBLISH_ENABLED=false`.

At audit time, the repository variables API returned only `NUGET_PUBLISH_ENABLED=true`; weekly activation remains off. This audit changed no workflow, variable, release or other repository. All GitHub operations were reads.

## Reproduction

Use authenticated `gh api orgs/PinguApps/repos --paginate` to enumerate repositories. For each default branch, read `repos/{owner}/{repo}/git/trees/{branch}?recursive=1`, check errors/truncation, then fetch each `.github/workflows/*.yml` or `.yaml` blob by SHA. Inspect all triggers and publishing calls, recording every cron and immutable commit. Compare all Sunday and daily start times against 10:17 UTC; include guarded publishers and distinguish archived repositories and non-publishing maintenance. Check repository Actions variables without changing them.

Source: [agreed requirements](https://linear.app/pinguapps/document/agreed-scope-and-delivery-requirements-92ad0139880c), PIN-1282, PIN-1276 and the existing publishing guide. Historical reference snapshots inform workflow roles; only the fresh inventory above informs this decision.
