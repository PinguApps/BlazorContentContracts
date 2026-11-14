param(
    [Parameter(Mandatory)][string] $Directory,
    [Parameter(Mandatory)][string] $Repository,
    [Parameter(Mandatory)][string] $Source,
    [Parameter(Mandatory)][string] $Version
)

$ErrorActionPreference = 'Stop'
$manifest = Get-Content (Join-Path $Directory manifest.json) -Raw | ConvertFrom-Json
if ($manifest.repository -cne $Repository -or $manifest.source -cne $Source -or $manifest.version -cne $Version) {
    throw 'Artifact identity mismatch.'
}
$expected = foreach ($id in @('PinguApps.BlazorContentContracts', 'PinguApps.BlazorContentContracts.Analyzers')) {
    foreach ($extension in @('nupkg', 'snupkg')) { "$id.$Version.$extension" }
}
if (@($manifest.files).Count -ne 4 -or (Compare-Object -CaseSensitive ($expected | Sort-Object) ($manifest.files.name | Sort-Object))) {
    throw 'Unexpected package set.'
}
$actual = @(Get-ChildItem -LiteralPath $Directory -File | Where-Object Name -ne manifest.json | Select-Object -ExpandProperty Name)
if (Compare-Object -CaseSensitive ($expected | Sort-Object) ($actual | Sort-Object)) {
    throw 'Unexpected artifact files.'
}
foreach ($file in $manifest.files) {
    if ((Get-FileHash (Join-Path $Directory $file.name) -Algorithm SHA256).Hash -cne $file.sha256) {
        throw 'Artifact hash mismatch.'
    }
}
