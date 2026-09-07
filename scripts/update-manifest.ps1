param(
    [string]$Repository,
    [string]$Tag,
    [string]$ArchivePath,
    [string]$OutputPath,
    [string]$Changelog,
    [DateTimeOffset]$Timestamp = [DateTimeOffset]::UtcNow
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $Repository) {
    $remote = git -C $root remote get-url origin
    if ($LASTEXITCODE -ne 0 -or $remote -notmatch 'github\.com[:/](?<repo>[^/]+/[^/]+?)(?:\.git)?$') {
        throw 'Pass -Repository owner/repository for the GitHub repository hosting releases.'
    }
    $Repository = $Matches.repo
}
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Repository must be owner/repository.' }
if (-not $ArchivePath) { $ArchivePath = Join-Path $root 'artifacts/Jellyfin.Jimakufin.zip' }
if (-not $OutputPath) { $OutputPath = Join-Path $root 'manifest.json' }

[xml]$project = Get-Content (Join-Path $root 'Jellyfin.Jimakufin/Jellyfin.Jimakufin.csproj') -Raw
$version = [version]$project.Project.PropertyGroup.Version
$versionString = '{0}.{1}.{2}.{3}' -f $version.Major, $version.Minor, [Math]::Max(0, $version.Build), [Math]::Max(0, $version.Revision)
if ([string]::IsNullOrWhiteSpace($Changelog)) {
    $Changelog = Get-Content (Join-Path $root "release-notes/$($project.Project.PropertyGroup.Version).md") -Raw
}
if (-not $Tag) { $Tag = "v$($project.Project.PropertyGroup.Version)" }
if ($Tag -notmatch '^v?\d+\.\d+\.\d+(\.\d+)?$' -or [version]$Tag.TrimStart('v') -ne $version) {
    throw 'The release tag must match the Jellyfin project Version, optionally prefixed with v.'
}
$api = @($project.Project.ItemGroup.PackageReference | Where-Object Include -eq 'Jellyfin.Controller')[0].Version
$abi = [version]$api
$targetAbi = '{0}.{1}.{2}.0' -f $abi.Major, $abi.Minor, $abi.Build

# Validate the archive before advertising it. Never hash a freshly rebuilt substitute
# for a ZIP that has already been uploaded: Jellyfin checks the exact archive bytes.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ArchivePath))
try {
    $names = @($archive.Entries.FullName | Sort-Object)
    if (($names -join ',') -ne 'Jellyfin.Jimakufin.dll') { throw 'Expected a single Jellyfin.Jimakufin.dll in the plugin archive.' }
    $temporaryDll = [System.IO.Path]::GetTempFileName()
    try {
        $inputStream = $archive.Entries[0].Open()
        try {
            $outputStream = [System.IO.File]::OpenWrite($temporaryDll)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
        } finally { $inputStream.Dispose() }
        $assembly = [System.Reflection.AssemblyName]::GetAssemblyName($temporaryDll)
        if ($assembly.Name -ne 'Jellyfin.Jimakufin' -or $assembly.Version.ToString() -ne $versionString) {
            throw 'The packaged assembly version does not match the release. Rebuild before generating a manifest.'
        }
    } finally { Remove-Item -LiteralPath $temporaryDll -Force }
} finally { $archive.Dispose() }
$checksum = (Get-FileHash -LiteralPath $ArchivePath -Algorithm MD5).Hash.ToLowerInvariant()
$guid = '7fafdaef-3f55-4fd7-b14d-bbfcdaa801ee'
$previous = @()
$existingPath = if (Test-Path -LiteralPath $OutputPath) { $OutputPath } else { Join-Path $root 'manifest.json' }
if (Test-Path -LiteralPath $existingPath) {
    $catalog = @(Get-Content -LiteralPath $existingPath -Raw | ConvertFrom-Json)
    $previous = @($catalog | Where-Object guid -eq $guid | ForEach-Object versions | Where-Object version -ne $versionString)
}
$entry = [ordered]@{
    version = $versionString
    changelog = $Changelog
    targetAbi = $targetAbi
    sourceUrl = "https://github.com/$Repository/releases/download/$Tag/Jellyfin.Jimakufin.zip"
    checksum = $checksum
    timestamp = $Timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}
$plugin = [ordered]@{
    guid = $guid
    name = 'Jimakufin'
    description = 'Japanese episode subtitles from Jimaku.cc, matched using TVDB and AniList metadata. Requires a Jimaku API key.'
    overview = 'Japanese subtitles from Jimaku.cc'
    owner = $Repository.Split('/')[0]
    category = 'Subtitles'
    versions = @(@($entry) + $previous | Sort-Object { [version]$_.version } -Descending)
}
ConvertTo-Json -InputObject @($plugin) -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Host "Updated $OutputPath for $versionString ($checksum)"
