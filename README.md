# Jimakufin

Fetch Japanese episode subtitles from [Jimaku.cc](https://jimaku.cc) in Emby or Jellyfin.

## Install from the Jellyfin catalog

After the first Jimakufin release is published, open **Dashboard → Plugins → Repositories → Add** and enter:

- **Name:** Jimakufin
- **Repository URL:** `https://github.com/bpwhelan/Emby.Jimaku/releases/latest/download/manifest.json`

Open **Catalog**, install **Jimakufin**, then restart Jellyfin. Enter your API key under **Dashboard → Plugins → Jimakufin** and enable **Jimakufin** with **Japanese** in your TV library's subtitle settings.

The release workflow maintains this manifest alongside the exact ZIP it describes. The URL becomes usable once a release containing these assets exists; checking in the JSON alone does not publish the plugin ZIP.

The GitHub repository is currently named `Emby.Jimaku`. **Jimakufin** is the new project/catalog name. If you rename the GitHub repository to `Jimakufin`, use `https://github.com/bpwhelan/Jimakufin/releases/latest/download/manifest.json` instead. The workflow derives release URLs from the repository running it.

## Supported servers

| Server | Project | Target |
| --- | --- | --- |
| Emby 4.8 | `Emby.Jimaku.csproj` | .NET Standard 2.0; existing Emby plugin ID and settings retained |
| Jellyfin 10.11.x | `Jellyfin.Jimakufin/Jellyfin.Jimakufin.csproj` | .NET 9; Jellyfin API 10.11.0 |

The two plugins use `Shared/Jimaku.Shared.csproj` for Jimaku requests, TVDB-to-AniList mapping, subtitle IDs, and downloads. Server dependencies and configuration pages remain in their respective projects. Jellyfin 10.10 and earlier are not targeted by this build.

## Installation

1. Download the ZIP for your server from this project's releases, or build it using the instructions below.
2. Stop the server and extract **both DLLs** from the ZIP into the plugin location:
   - **Emby:** the `plugins` folder inside your server's program data directory. On a typical Windows installation this is `%APPDATA%/Emby-Server/programdata/plugins/`.
   - **Jellyfin:** create a `Jimaku` folder inside your server's `plugins` directory, then extract there. For example, `/var/lib/jellyfin/plugins/Jimaku/` on Linux or `/config/plugins/Jimaku/` with the standard Docker configuration mount.
3. Restart the server.
4. Open **Dashboard → Plugins → Jimakufin** (or **Jimaku** in Emby), enter your Jimaku API key, and save.
5. In your TV library's subtitle settings, enable that provider and select **Japanese** as a subtitle download language.

Each ZIP contains its server's plugin DLL plus `Jimaku.Shared.dll`. When upgrading an existing Emby installation, add the shared DLL alongside the replacement `Emby.Jimaku.dll`. Install only the ZIP matching your server; do not copy build-time `MediaBrowser.*` or `Jellyfin.*` SDK dependencies into the plugins folder.

See Jellyfin's [plugin installation documentation](https://jellyfin.org/docs/general/server/plugins/) for locating its plugin directory.

## Jimaku API key

Create an account at [Jimaku.cc](https://jimaku.cc), open your account settings, and generate an API key. Paste it into the plugin configuration. Saved key changes apply to subsequent API requests without a server restart.

## Usage and matching

Use the server's subtitle search for an episode, or its scheduled subtitle download task. The plugin returns Japanese subtitles when Jimaku has a matching entry and episode.

Matching requires a **TVDB ID on the parent series**, a season number, and an episode number. The shared client uses [Kometa's Anime-IDs mappings](https://github.com/Kometa-Team/Anime-IDs) to find the AniList ID. Jellyfin resolves the parent series from the library episode's media path; the episode's own TVDB ID is not used as a series ID.

For split seasons, the mapping's episode offset is subtracted before searching Jimaku. Missing metadata, absent mappings, and requests for other languages return no results. Network and authentication errors are reported to the server. Movies and title-only searches are not currently supported.

## Build and test

Install the .NET 9 SDK (or a newer SDK with .NET 9 runtime available for tests).

```powershell
dotnet build Jimakufin.sln -c Release
dotnet test Tests/Jimaku.Tests.csproj -c Release --no-build
pwsh ./scripts/package.ps1 -NoBuild
```

Packages are written to `artifacts/Emby.Jimaku.zip` and `artifacts/Jellyfin.Jimakufin.zip`. GitHub Actions builds both plugins, runs the tests, and uploads separate packages.

Ordinary builds do not install anything. To explicitly copy the Emby plugin and shared DLL to the local Windows Emby installation:

```powershell
dotnet build Emby.Jimaku.csproj -c Release -p:DeployToEmby=true
```

The tests use simulated HTTP responses to check mapping, episode offsets, language and metadata filtering, API key changes, legacy subtitle IDs, downloads, errors, and cancellation. A live server and Jimaku account are needed to validate loading, the settings page, and end-to-end downloads in your installation.

## Publishing releases and the repository JSON

The checked-in `manifest.json` is a Jellyfin repository catalog with the plugin GUID, four-part version, minimum Jellyfin ABI, release ZIP URL, and MD5 checksum. The GUID stays unchanged when the project is renamed, so Jellyfin recognizes future versions as updates. Its initial entry describes the locally generated `v1.0.1` package; that release is not published by these scripts.

For automated releases:

1. Set the Jellyfin project's `Version` (currently `1.0.1`) and commit the changes.
2. Publish a stable GitHub release with a matching tag, such as `v1.0.1` or `1.0.1`.
3. The release workflow builds/tests both plugins and uploads `Emby.Jimaku.zip`, `Jellyfin.Jimakufin.zip`, and `manifest.json`. Wait for that workflow to finish before installing.

The recommended repository URL above always serves the latest release's generated manifest and needs no automated commits to your default branch. To keep the checked-in snapshot synchronized, download `manifest.json` from the release and commit it. This also preserves older version entries in subsequent releases.

If you prefer a repository URL pointing directly to the checked-in file, use `https://raw.githubusercontent.com/bpwhelan/Emby.Jimaku/main/manifest.json`. Keep that file synchronized with the published release's manifest; a stale checksum prevents installation. Update the repository segment if you rename it.

For a manual release, build/package once, then generate the catalog from the **exact ZIP you will upload**:

```powershell
pwsh ./scripts/package.ps1
pwsh ./scripts/update-manifest.ps1 -Tag v1.0.1
```

Upload `artifacts/Jellyfin.Jimakufin.zip`, `artifacts/Emby.Jimaku.zip`, and the generated root `manifest.json` to that tag's release, then check in `manifest.json`. Do not rebuild or repackage the ZIP afterward without regenerating the manifest. Use `-Repository bpwhelan/Jimakufin` when preparing URLs for a renamed repository before updating the local Git remote. The script validates the tag against the project version and retains older catalog versions.

The manifest format follows Jellyfin's [plugin repository documentation](https://jellyfin.org/posts/plugin-updates/).

## Support

Open a GitHub issue or contact @Beangate on Discord.

If this plugin helps you, consider supporting the work through [GitHub Sponsors](https://github.com/sponsors/bpwhelan) or [Ko-fi](https://ko-fi.com/beangate).
