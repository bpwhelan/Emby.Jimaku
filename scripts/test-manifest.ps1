param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [string]$ArchivePath
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $ArchivePath) { $ArchivePath = Join-Path $root 'artifacts/Jellyfin.Jimakufin.zip' }
$raw = Get-Content -LiteralPath $ManifestPath -Raw
if (-not $raw.TrimStart().StartsWith('[')) { throw 'The repository manifest must be a JSON array.' }
$catalog = @($raw | ConvertFrom-Json)
if ($catalog.Count -ne 1 -or $catalog[0].guid -ne '7fafdaef-3f55-4fd7-b14d-bbfcdaa801ee' -or $catalog[0].name -ne 'Jimakufin') {
    throw 'Unexpected plugin identity in manifest.'
}
[xml]$project = Get-Content (Join-Path $root 'Jellyfin.Jimakufin/Jellyfin.Jimakufin.csproj') -Raw
$expected = [version]$project.Project.PropertyGroup.Version
$version = '{0}.{1}.{2}.{3}' -f $expected.Major, $expected.Minor, $expected.Build, [Math]::Max(0, $expected.Revision)
$entry = @($catalog[0].versions | Where-Object version -eq $version)
if ($entry.Count -ne 1 -or $catalog[0].versions[0].version -ne $version) { throw 'Current version must occur exactly once and be first.' }
$entry = $entry[0]
$apiVersion = [version]@($project.Project.ItemGroup.PackageReference | Where-Object Include -eq 'Jellyfin.Controller')[0].Version
if ($entry.targetAbi -ne "$apiVersion.0") { throw 'Manifest ABI does not match Jellyfin SDK.' }
if ($entry.sourceUrl -notmatch '^https://github\.com/[^/]+/[^/]+/releases/download/([^/]+)/Jellyfin\.Jimakufin\.zip$') { throw 'Invalid release asset URL.' }
if ([version]$Matches[1].TrimStart('v') -ne $expected) { throw 'Release URL tag does not match project version.' }
if ([string]::IsNullOrWhiteSpace($entry.changelog)) { throw 'Release notes are empty.' }
[void][DateTimeOffset]::Parse($entry.timestamp)
if ($entry.checksum -ne (Get-FileHash -LiteralPath $ArchivePath -Algorithm MD5).Hash.ToLowerInvariant()) { throw 'Manifest checksum does not match the release ZIP.' }
Write-Host "Validated manifest version $version and exact ZIP checksum."
