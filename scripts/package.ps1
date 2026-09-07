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
    $files = @((Join-Path $directory "$name.dll"))
    foreach ($file in $files) {
        if (-not (Test-Path -LiteralPath $file)) { throw "Missing build output: $file" }
    }
    # Fixed entry metadata makes repackaging the same DLL reproducible.
    $destination = [System.IO.File]::Open((Join-Path $output "$name.zip"), [System.IO.FileMode]::Create)
    try {
        $zip = [System.IO.Compression.ZipArchive]::new($destination, [System.IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            $entry = $zip.CreateEntry("$name.dll", [System.IO.Compression.CompressionLevel]::NoCompression)
            $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $entry.ExternalAttributes = 0
            $inputStream = [System.IO.File]::OpenRead($files[0])
            try {
                $entryStream = $entry.Open()
                try { $inputStream.CopyTo($entryStream) } finally { $entryStream.Dispose() }
            } finally { $inputStream.Dispose() }
        } finally { $zip.Dispose() }
    } finally { $destination.Dispose() }
}
