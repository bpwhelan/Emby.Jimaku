param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $NoBuild) {
    dotnet build (Join-Path $root 'Jimakufin.sln') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
$output = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $output | Out-Null
foreach ($server in @('Emby', 'Jellyfin')) {
    $name = if ($server -eq 'Emby') { 'Emby.Jimaku' } else { 'Jellyfin.Jimakufin' }
    $relative = if ($server -eq 'Emby') { 'bin/Release/netstandard2.0' } else { 'Jellyfin.Jimakufin/bin/Release/net9.0' }
    $directory = Join-Path $root $relative
    $files = @((Join-Path $directory "$name.dll"), (Join-Path $directory 'Jimaku.Shared.dll'))
    foreach ($file in $files) {
        if (-not (Test-Path -LiteralPath $file)) { throw "Missing build output: $file" }
    }
    Compress-Archive -LiteralPath $files -DestinationPath (Join-Path $output "$name.zip") -Force
}
