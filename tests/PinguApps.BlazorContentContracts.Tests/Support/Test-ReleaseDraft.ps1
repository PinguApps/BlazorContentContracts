param([Parameter(Mandatory)][string] $Scenario)

$ErrorActionPreference = 'Stop'
$git = (Get-Command git -CommandType Application).Source
$root = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $root | Out-Null
Push-Location $root
try {
    & $git init --quiet -b main
    & $git config commit.gpgsign false
    & $git config tag.gpgsign false
    & $git -c user.name=Fixture -c user.email=fixture@example.invalid commit --quiet --allow-empty -m baseline
    & $git tag v1.0.0
    $baseline = & $git rev-parse HEAD
    $file = switch ($Scenario) {
        'dependency' { 'Directory.Packages.props' }
        'docs' { 'README.md' }
        default { '.metadata' }
    }
    Set-Content $file 'merged maintenance'
    & $git add -- $file
    & $git -c user.name=Fixture -c user.email=fixture@example.invalid commit --quiet -m $Scenario
    $head = & $git rev-parse HEAD
    $stable = @{ id = 1; draft = $false; prerelease = $false; tag_name = 'v1.0.0' }
    $draft = @{ id = 2; draft = $true; prerelease = $false; tag_name = 'v1.1.0'; name = 'v1.1.0'; body = 'Maintenance'; target_commitish = 'main' }
    $releases = @($stable, $draft)
    switch ($Scenario) {
        'no commits' { & $git reset --quiet --hard $baseline }
        'no stable' { $releases = @($draft) }
        'no draft' { $releases = @($stable) }
        'multiple drafts' { $releases += $draft.Clone() }
        'prerelease' { $draft.prerelease = $true }
        'invalid tag' { $draft.tag_name = 'v1.1.0-beta' }
        'same version' { $draft.tag_name = $draft.name = 'v1.0.0' }
        'lower version' { $draft.tag_name = $draft.name = 'v0.9.0' }
        'outside main' { $draft.target_commitish = 'other' }
        'stale tag' { & $git tag v1.1.0 $baseline }
        'matching tag' { & $git tag v1.1.0 $head }
        'empty notes' { $draft.body = '' }
        'baseline outside' {
            & $git checkout --quiet --orphan unrelated
            & $git -c user.name=Fixture -c user.email=fixture@example.invalid commit --quiet --allow-empty -m unrelated
            & $git tag -f v1.0.0 | Out-Null
            & $git checkout --quiet main
        }
    }
    ConvertTo-Json -Depth 10 -InputObject $releases > releases.json
    & "$PSScriptRoot/../../../scripts/Select-ReleaseDraft.ps1" -ReleasesFile releases.json -Main refs/heads/main
}
finally {
    Pop-Location
    $resolved = [IO.Path]::GetFullPath($root)
    $temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (!$resolved.StartsWith($temporary, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Fixture cleanup must remain inside the temporary directory.'
    }
    Remove-Item -LiteralPath $root -Recurse -Force
}


