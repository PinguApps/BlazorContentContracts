param(
    [Parameter(Mandatory)][string] $Tag,
    [string] $Name
)

$ErrorActionPreference = 'Stop'
if ($Tag -cnotmatch '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw 'Stable releases require an exact vMAJOR.MINOR.PATCH tag without leading zeroes.'
}
$version = $Tag.Substring(1)
if ($PSBoundParameters.ContainsKey('Name') -and $Name -cne $Tag -and $Name -cne $version) {
    throw 'Release name and tag must describe the same version.'
}
if ($version.StartsWith('0.')) {
    throw 'Stable publication requires version 1.0.0 or later.'
}
$version
