param(
    [Parameter(Mandatory)][string] $ReleasesFile,
    [string] $Main = 'refs/remotes/origin/main'
)

$ErrorActionPreference = 'Stop'
$releases = @(Get-Content -Raw $ReleasesFile | ConvertFrom-Json)
$stable = @($releases | Where-Object { -not $_.draft -and -not $_.prerelease })
if ($stable.Count -eq 0) { return }

# Compare against the greatest stable version, not API ordering or publication date.
$versions = @($stable | ForEach-Object {
    [version] (& "$PSScriptRoot/Get-ReleaseVersion.ps1" -Tag $_.tag_name)
})
$latestVersion = $versions | Sort-Object -Descending | Select-Object -First 1
$latest = $stable | Where-Object { $_.tag_name -ceq "v$latestVersion" } | Select-Object -First 1
$baseline = git rev-parse --verify "refs/tags/$($latest.tag_name)^{commit}"
if ($LASTEXITCODE -ne 0) { throw 'Stable tag must resolve to a commit.' }
git merge-base --is-ancestor $baseline $Main
if ($LASTEXITCODE -ne 0) { throw 'Stable tag must be on main.' }
$head = git rev-parse --verify "$Main^{commit}"
if ($LASTEXITCODE -ne 0) { throw 'Main must resolve to a commit.' }
if ($baseline -eq $head) { return }

$drafts = @($releases | Where-Object draft)
if ($drafts.Count -eq 0) { return }
if ($drafts.Count -ne 1) { throw 'Multiple drafts; refusing to choose.' }
$draft = $drafts[0]
if ($draft.prerelease) { throw 'Draft must be stable.' }
$version = [version] (& "$PSScriptRoot/Get-ReleaseVersion.ps1" -Tag $draft.tag_name -Name $draft.name)
if ($version -le $latestVersion) { throw 'Draft version must increase.' }
if ([string]::IsNullOrWhiteSpace($draft.body)) { throw 'Release notes are required by the publisher.' }
if ($draft.target_commitish -cne 'main' -and $draft.target_commitish -cne $head) {
    throw 'Draft must target current main.'
}

# Existing tags are never moved; require them to capture the current main snapshot.
$tagSource = git rev-parse --verify --quiet "refs/tags/$($draft.tag_name)^{commit}"
if ($LASTEXITCODE -eq 0 -and $tagSource -cne $head) {
    throw 'Existing draft tag must identify current main.'
}
if (@($releases | Where-Object { -not $_.draft -and $_.tag_name -ceq $draft.tag_name }).Count -ne 0) {
    throw 'Draft tag is already published.'
}
if ("$($draft.id)" -notmatch '^[1-9][0-9]*$') { throw 'Invalid release ID.' }

[pscustomobject]@{ id = $draft.id; tag = $draft.tag_name; source = $head } | ConvertTo-Json -Compress
