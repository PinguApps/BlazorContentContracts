param(
    [string] $Destination,
    [string] $SdkVersion = '10.0.402'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$feed = Join-Path $root 'artifacts/packages'

foreach ($name in @('PinguApps.BlazorContentContracts', 'PinguApps.BlazorContentContracts.Analyzers')) {
    if (-not (Test-Path -LiteralPath (Join-Path $feed "$name.1.0.0.nupkg"))) {
        throw 'Run scripts/verify.cs to prepare the exact local packages first.'
    }
}

if (-not $Destination) {
    $Destination = Join-Path $root ('artifacts/editor-smoke-' + [Guid]::NewGuid().ToString('N'))
}

if (Test-Path -LiteralPath $Destination) {
    throw 'Choose a new destination to preserve existing editor sessions and package caches.'
}

New-Item -ItemType Directory -Path $Destination | Out-Null
$fixtures = Join-Path $root 'samples/EditorDiagnostics'
Get-ChildItem -LiteralPath $fixtures -Recurse -File | Where-Object {
    $_.Extension -in @('.cs', '.csproj', '.razor') -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
} | ForEach-Object {
    $target = Join-Path $Destination ([IO.Path]::GetRelativePath($fixtures, $_.FullName))
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $target
}
Copy-Item -LiteralPath (Join-Path $root 'Directory.Packages.props') -Destination $Destination
$config = [System.Security.SecurityElement]::Escape($feed)
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="local" value="$config" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="PinguApps.BlazorContentContracts*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="Microsoft.*" /><package pattern="System.*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $Destination 'NuGet.Config')
@'
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <RestorePackagesPath>$(MSBuildThisFileDirectory)packages</RestorePackagesPath>
    <ErrorLog>$(MSBuildProjectDirectory)/diagnostics.sarif</ErrorLog>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $Destination 'Directory.Build.props')
@{ sdk = @{ version = $SdkVersion; rollForward = 'disable'; allowPrerelease = $false } } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Destination 'global.json')
New-Item -ItemType Directory -Path (Join-Path $Destination '.vscode') | Out-Null
'{"razor.advanced.showAllCSharpCodeActions":true}' | Set-Content -LiteralPath (Join-Path $Destination '.vscode/settings.json')
Write-Output ([IO.Path]::GetFullPath($Destination))
