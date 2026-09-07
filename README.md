# Jimakufin

Fetch Japanese episode subtitles from [Jimaku.cc](https://jimaku.cc) in Emby or Jellyfin.

Jellyfin is very recent, and extremely vibe coded (the original emby plugin was not), so it may not work properly...

## Install from the Jellyfin catalog

After the first Jimakufin release is published, open **Dashboard → Plugins → Repositories → Add** and enter:

- **Name:** Jimakufin
- **Repository URL:** `https://github.com/bpwhelan/Jimakufin/releases/latest/download/manifest.json`

Open **Catalog**, install **Jimakufin**, then restart Jellyfin. Enter your API key under **Dashboard → Plugins → Jimakufin** and enable **Jimakufin** with **Japanese** in your TV library's subtitle settings.

The release workflow maintains this manifest alongside the exact ZIP it describes. The URL becomes usable once a release containing these assets exists; checking in the JSON alone does not publish the plugin ZIP.

The GitHub repository is **bpwhelan/Jimakufin** (formerly `Emby.Jimaku`). The workflow derives release URLs from the repository running it.

## Supported servers

| Server | Project | Target |
| --- | --- | --- |
| Emby 4.8 | `Emby.Jimaku.csproj` | .NET Standard 2.0; existing Emby plugin ID and settings retained |
| Jellyfin 10.11.x | `Jellyfin.Jimakufin/Jellyfin.Jimakufin.csproj` | .NET 9; Jellyfin API 10.11.0 |

The two plugins compile the same Jimaku client and models into their own assemblies through `Shared/Jimaku.Shared.targets`. Each plugin is a **single DLL**, with no separate shared library to load. Server APIs are supplied by Emby or Jellyfin. Jellyfin 10.10 and earlier are not targeted by this build.

## Installation

1. Download the ZIP for your server from this project's releases, or build it using the instructions below.
2. Stop the server and extract the **single DLL** from the ZIP into the plugin location:
   - **Emby:** the `plugins` folder inside your server's program data directory. On a typical Windows installation this is `%APPDATA%/Emby-Server/programdata/plugins/`.
   - **Jellyfin:** create a `Jimakufin_1.0.4.0` folder inside your server's actual `plugins` directory, then extract there. Use the directory shown in your Jellyfin startup log (for example, `/config/data/plugins/`).
3. Restart the server.
4. Open **Dashboard → Plugins → Jimakufin** (or **Jimaku** in Emby), enter your Jimaku API key, and save.
5. In your TV library's subtitle settings, enable that provider and select **Japanese** as a subtitle download language.

Each ZIP contains only its server's plugin DLL: `Emby.Jimaku.dll` or `Jellyfin.Jimakufin.dll`. Install only the ZIP matching your server; do not copy build-time SDK dependencies into the plugins folder.

### Upgrading from the two-DLL builds

With the server stopped, move the old Jimakufin plugin folders/DLLs out of the server's scanned plugin directory and install the new single-DLL package. Remove the old `Jimaku.Shared.dll` supplied by this plugin as well. Keep the server's plugin configuration XML files so the API key is retained. On Jellyfin, use a fresh version folder so the previous failed installation's disabled state is not carried over. After restarting, confirm the plugin is active and enabled as a subtitle provider for your TV library.

Look for `Jimakufin Jellyfin plugin 1.0.4.0 loaded (single DLL)` or `Jimakufin Emby plugin 1.0.2... loaded (single DLL)` in the startup log. On Jellyfin, also look for `Jimakufin subtitle provider initialized` and confirm **Jimakufin** appears under the TV library's **Subtitle downloaders**.

See Jellyfin's [plugin installation documentation](https://jellyfin.org/docs/general/server/plugins/) for locating its plugin directory.

## Jimaku API key

Create an account at [Jimaku.cc](https://jimaku.cc), open your account settings, and generate an API key. Paste it into the plugin configuration. Saved key changes apply to subsequent API requests without a server restart.

## Usage and matching

Use the server's subtitle search for an episode, or its scheduled subtitle download task. The plugin returns Japanese subtitles when Jimaku has a matching entry and episode.

Matching requires a **TVDB ID on the parent series**, a season number, and an episode number. The shared client uses [Kometa's Anime-IDs mappings](https://github.com/Kometa-Team/Anime-IDs) to find the AniList ID. Jellyfin resolves the parent series from the library episode's media path; the episode's own TVDB ID is not used as a series ID.

For split seasons, the mapping's episode offset is subtracted before searching Jimaku. Missing metadata, absent mappings, and requests for other languages return no results. Network and authentication errors are reported to the server. Movies and title-only searches are not currently supported.

## Troubleshooting empty searches

Search either server's log for `Jimakufin`. Both plugins write startup, provider initialization, search inputs, series lookup, mapping decisions, HTTP status codes, and result counts at **Information** level. The Jellyfin media path is only logged at Debug level. API keys, authorization headers, and response bodies are not logged.

- No startup message after restarting: check the installed DLL/version and look for assembly load errors. Jellyfin must list the plugin as active/compatible; reinstall into a fresh plugin folder if an earlier load error disabled it.
- Plugin loads but is absent from Jellyfin's Subtitle downloaders: upgrade to Jellyfin plugin 1.0.3 or later. Earlier builds omitted the service registration required by Jellyfin 10.11, so the subtitle manager never received the provider. This is independent of the API key or series metadata.
- Initialization appears, but no search message: confirm Jimakufin is enabled for that TV library and perform a manual Japanese subtitle search on an episode. Movies are not supported.
- Missing series TVDB ID: edit the **parent series** metadata and populate its TheTVDB ID. Episode IDs and TMDB IDs do not substitute for the series TVDB ID.
- No AniList mapping: the logged TVDB ID/season/episode combination could not be matched in the Anime-IDs dataset.
- HTTP 401/403: check Jimaku authentication/access; HTTP 429 indicates rate limiting. The logged URL identifies whether Jimaku or the mapping host failed.
- Zero entries or files: the request reached Jimaku successfully, but no corresponding entry or episode files were returned.

When sharing logs for troubleshooting, include the lines from `Jimakufin search` through the final result/error for one episode.

## Build and test

Install PowerShell 7 and the .NET SDK selected by `global.json` (9.0.317, with patch roll-forward). The SDK includes the .NET 9 and ASP.NET Core 9 runtimes needed by the load tests.

```powershell
pwsh ./scripts/validate.ps1 -Repository bpwhelan/Jimakufin -Tag v1.0.4
```

This runs the same pipeline as CI: locked NuGet restore, warnings-as-errors build, packaging, manifest validation, release-tool rejection checks, and all tests against the actual ZIPs. Packages and the generated manifest are written to `artifacts/`; test reports are in `artifacts/test-results/`. CI validates on Windows and Linux and lints the GitHub workflows. Update and commit `packages.lock.json` files with `dotnet restore Jimakufin.sln --force-evaluate` when deliberately changing dependencies.

Ordinary builds do not install anything. To explicitly copy the Emby plugin DLL to the local Windows Emby installation:

```powershell
dotnet build Emby.Jimaku.csproj -c Release -p:DeployToEmby=true
```

The tests use simulated HTTP responses to check mapping, episode offsets, language and metadata filtering, API key changes, legacy subtitle IDs, downloads, errors, and cancellation. They also load the Jellyfin assembly, invoke its service-registration hook, and resolve the subtitle provider through dependency injection. Both plugin assemblies are checked for embedded client code without a `Jimaku.Shared` reference. CI checks the actual ZIPs contain exactly one DLL. A live server and Jimaku account are still needed to validate your installation and authenticated downloads.

## Publishing releases and the repository JSON

The checked-in `manifest.json` is a Jellyfin repository catalog with the plugin GUID, four-part version, minimum Jellyfin ABI, release ZIP URL, and MD5 checksum. The GUID stays unchanged when the project is renamed, so Jellyfin recognizes future versions as updates. The newest entry describes the locally generated `v1.0.4` package; generating the manifest does not publish a release.

For automated releases:

After committing the version bump and `release-notes/<version>.md` on `main`, run:

```powershell
pwsh ./scripts/release.ps1
```

Requires GitHub CLI authentication (`gh auth login`), Git, PowerShell 7, and the SDK from `global.json`. The script validates locally, pushes main, waits for CI, pushes a new annotated version tag, publishes the release, waits for asset upload, verifies the downloaded checksum, and commits/pushes the published manifest back to main. It refuses dirty working trees and existing remote tags. If a release fails partway through, inspect that workflow before retrying; the script does not overwrite published tags or assets.

The equivalent manual steps are:

1. Set the Jellyfin project's `Version` (currently `1.0.4`), write `release-notes/1.0.4.md`, and commit the changes and lockfiles.
2. Publish a stable GitHub release with a matching tag, such as `v1.0.4` or `1.0.4`.
3. The release workflow validates on Windows and Linux, then uploads the exact verified Linux artifacts: `Emby.Jimaku.zip`, `Jellyfin.Jimakufin.zip`, and finally `manifest.json`. Wait for that workflow to finish before installing. It does not rebuild during publishing or overwrite existing assets; investigate a failed/partial upload before retrying. Prereleases are not published to this stable catalog.

The recommended repository URL above always serves the latest release's generated manifest and needs no automated commits to your default branch. To keep the checked-in snapshot synchronized, download `manifest.json` from the release and commit it. This also preserves older version entries in subsequent releases.

If you prefer a repository URL pointing directly to the checked-in file, use `https://raw.githubusercontent.com/bpwhelan/Jimakufin/main/manifest.json`. Keep that file synchronized with the published release's manifest; a stale checksum prevents installation. The checked-in release candidate describes local build bytes; CI generates a new manifest for its exact ZIP. After publishing, copy the release's manifest back into the repository.

For a manual release, build/package once, then generate the catalog from the **exact ZIP you will upload**:

```powershell
pwsh ./scripts/validate.ps1 -Repository bpwhelan/Jimakufin -Tag v1.0.4
Copy-Item artifacts/manifest.json manifest.json
```

For a manual upload, use these exact ZIPs and manifest, and avoid racing the automatic release workflow. For the normal automated process, let the workflow supply all release assets. Do not rebuild the DLL afterward without regenerating the manifest. The script checks the DLL version inside the ZIP and retains older catalog versions. Only published versions should be retained; the current manifest carries the verified 1.0.1 release entry and the 1.0.4 candidate, excluding the unpublished troubleshooting builds.

The manifest format follows Jellyfin's [plugin repository documentation](https://jellyfin.org/posts/plugin-updates/).

## Support

Open a GitHub issue or contact @Beangate on Discord.

If this plugin helps you, consider supporting the work through [GitHub Sponsors](https://github.com/sponsors/bpwhelan) or [Ko-fi](https://ko-fi.com/beangate).
