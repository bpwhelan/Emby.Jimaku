$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $root 'artifacts/release-tool-tests'
New-Item -ItemType Directory -Force $scratch | Out-Null
function Assert-Rejected([scriptblock]$Action, [string]$Scenario) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw "Release validation accepted $Scenario." }
}
$manifestPath = Join-Path $root 'artifacts/manifest.json'
$badManifest = Join-Path $scratch 'invalid-manifest.json'
foreach ($field in @('checksum', 'version', 'targetAbi', 'sourceUrl')) {
    $catalog = @(Get-Content $manifestPath -Raw | ConvertFrom-Json)
    $catalog[0].versions[0].$field = 'invalid'
    ConvertTo-Json -InputObject $catalog -Depth 10 | Set-Content $badManifest
    Assert-Rejected { & "$PSScriptRoot/test-manifest.ps1" -ManifestPath $badManifest } "an invalid $field"
}
Assert-Rejected { & "$PSScriptRoot/update-manifest.ps1" -Repository bpwhelan/Jimakufin -Tag v99.0.0 -OutputPath $badManifest } 'a mismatched release tag'
$badArchive = Join-Path $scratch 'invalid-package.zip'
$destination = [System.IO.File]::Open($badArchive, [System.IO.FileMode]::Create)
try {
    $zip = [System.IO.Compression.ZipArchive]::new($destination, [System.IO.Compression.ZipArchiveMode]::Create, $true)
    try { [void]$zip.CreateEntry('Jimaku.Shared.dll') } finally { $zip.Dispose() }
} finally { $destination.Dispose() }
Assert-Rejected { & "$PSScriptRoot/update-manifest.ps1" -Repository bpwhelan/Jimakufin -ArchivePath $badArchive -OutputPath $badManifest } 'a wrongly packaged DLL'
$before = @('Emby.Jimaku.zip', 'Jellyfin.Jimakufin.zip') | ForEach-Object { (Get-FileHash (Join-Path $root "artifacts/$_") -Algorithm SHA256).Hash }
& "$PSScriptRoot/package.ps1" -NoBuild
$after = @('Emby.Jimaku.zip', 'Jellyfin.Jimakufin.zip') | ForEach-Object { (Get-FileHash (Join-Path $root "artifacts/$_") -Algorithm SHA256).Hash }
if (($before -join ',') -ne ($after -join ',')) { throw 'Repackaging the same DLLs changed ZIP checksums.' }
Write-Host 'Release tool rejection checks and reproducible packaging passed.'
