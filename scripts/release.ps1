# Run after committing the version bump and release notes on the default branch.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed (exit $LASTEXITCODE)." }
}
function Wait-Workflow([string]$Workflow, [string]$Commit, [string]$Event) {
    $run = $null
    for ($attempt = 0; $attempt -lt 24; $attempt++) {
        $runs = Invoke-Checked gh @('run', 'list', '--workflow', $Workflow, '--commit', $Commit, '--event', $Event, '--limit', '1', '--json', 'databaseId') | ConvertFrom-Json
        if ($runs.Count -gt 0) { $run = $runs[0].databaseId; break }
        Start-Sleep -Seconds 5
    }
    if (-not $run) { throw "No $Workflow run appeared for $Commit. Check GitHub Actions before continuing." }
    Invoke-Checked gh @('run', 'watch', "$run", '--exit-status', '--interval', '10')
}
Push-Location $root
try {
    if (Invoke-Checked git @('status', '--porcelain')) { throw 'Commit or stash working-tree changes before releasing.' }
    $repository = Invoke-Checked gh @('repo', 'view', '--json', 'nameWithOwner,defaultBranchRef') | ConvertFrom-Json
    $branch = Invoke-Checked git @('branch', '--show-current')
    if ($branch -ne $repository.defaultBranchRef.name) { throw 'Run releases from the repository default branch.' }
    Invoke-Checked git @('fetch', 'origin')
    Invoke-Checked git @('merge', '--ff-only', "origin/$branch")
    [xml]$project = Get-Content Jellyfin.Jimakufin/Jellyfin.Jimakufin.csproj -Raw
    $version = $project.Project.PropertyGroup.Version
    $tag = "v$version"
    $notes = "release-notes/$version.md"
    if (-not (Test-Path $notes)) { throw "Missing release notes: $notes" }
    if (Invoke-Checked git @('ls-remote', '--tags', 'origin', "refs/tags/$tag")) { throw "$tag already exists. Use a new version; this script never replaces tags." }
    & "$PSScriptRoot/validate.ps1" -Repository $repository.nameWithOwner -Tag $tag
    Invoke-Checked git @('push', 'origin', $branch)
    $commit = Invoke-Checked git @('rev-parse', 'HEAD')
    Wait-Workflow 'dotnet.yml' $commit 'push'
    Invoke-Checked git @('tag', '-a', $tag, '-m', "Jimakufin $version")
    Invoke-Checked git @('push', 'origin', "refs/tags/$tag")
    Invoke-Checked gh @('release', 'create', $tag, '--verify-tag', '--title', "Jimakufin $version", '--notes-file', $notes)
    Wait-Workflow 'release.yml' $commit 'release'
    $download = "artifacts/published-$tag"
    Invoke-Checked gh @('release', 'download', $tag, '--pattern', 'manifest.json', '--pattern', 'Jellyfin.Jimakufin.zip', '--dir', $download, '--clobber')
    & "$PSScriptRoot/test-manifest.ps1" -ManifestPath "$download/manifest.json" -ArchivePath "$download/Jellyfin.Jimakufin.zip"
    Copy-Item -LiteralPath "$download/manifest.json" -Destination manifest.json
    if (Invoke-Checked git @('diff', '--name-only', '--', 'manifest.json')) {
        Invoke-Checked git @('add', '--', 'manifest.json')
        Invoke-Checked git @('commit', '-m', "Sync Jellyfin manifest for $tag")
        Invoke-Checked git @('push', 'origin', $branch)
    }
    Invoke-Checked gh @('release', 'view', $tag, '--json', 'url', '--jq', '.url')
} finally { Pop-Location }
