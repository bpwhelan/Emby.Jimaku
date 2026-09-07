param(
    [string]$Repository = 'bpwhelan/Jimakufin',
    [string]$Tag,
    [string]$Changelog,
    [DateTimeOffset]$Timestamp = [DateTimeOffset]::UtcNow
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
$oldPackage = $env:JIMAKU_TEST_PACKAGE
$oldEmbyPackage = $env:EMBY_TEST_PACKAGE
try {
    dotnet restore Jimakufin.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    dotnet build Jimakufin.sln -c Release --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & "$PSScriptRoot/package.ps1" -NoBuild
    $manifestArgs = @{ Repository = $Repository; OutputPath = 'artifacts/manifest.json'; Timestamp = $Timestamp }
    if ($Tag) { $manifestArgs.Tag = $Tag }
    if ($Changelog) { $manifestArgs.Changelog = $Changelog }
    & "$PSScriptRoot/update-manifest.ps1" @manifestArgs
    & "$PSScriptRoot/test-manifest.ps1" -ManifestPath artifacts/manifest.json
    & "$PSScriptRoot/test-release-tools.ps1"
    $env:JIMAKU_TEST_PACKAGE = Join-Path $root 'artifacts/Jellyfin.Jimakufin.zip'
    $env:EMBY_TEST_PACKAGE = Join-Path $root 'artifacts/Emby.Jimaku.zip'
    dotnet test Tests/Jimaku.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
} finally {
    $env:JIMAKU_TEST_PACKAGE = $oldPackage
    $env:EMBY_TEST_PACKAGE = $oldEmbyPackage
    Pop-Location
}
