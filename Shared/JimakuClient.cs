using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.Jimaku.Model;

namespace Jimaku.Shared
{
    // Use each server's serializer without shipping conflicting JSON dependencies.
    public interface IJsonCodec
    {
        T Deserialize<T>(string value);
        string Serialize<T>(T value);
    }

    public sealed class JimakuClient
    {
        private const string ApiRoot = "https://jimaku.cc/api/";
        private const string MappingUrl = "https://raw.githubusercontent.com/Kometa-Team/Anime-IDs/master/anime_ids.json";
        private readonly HttpClient http;
        private readonly IJsonCodec json;
        private readonly Func<string> apiKey;
        private readonly Action<string> diagnostic;

        public JimakuClient(HttpClient http, IJsonCodec json, Func<string> apiKey, Action<string> diagnostic = null)
        {
            this.http = http;
            this.json = json;
            this.apiKey = apiKey;
            this.diagnostic = diagnostic ?? (_ => { });
        }

        public static bool IsJapanese(string language) => string.IsNullOrWhiteSpace(language) ||
            string.Equals(language, "ja", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "jpn", StringComparison.OrdinalIgnoreCase);

        public async Task<IReadOnlyList<JimakuFile>> SearchAsync(string tvdbId, int? season, int? episode, string language, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsJapanese(language)) return Empty("Search skipped: requested language is not Japanese.");
            if (string.IsNullOrWhiteSpace(apiKey())) return Empty("Search skipped: API key is not configured.");
            if (!int.TryParse(tvdbId, out var tvdb)) return Empty("Search skipped: parent series has no valid TVDB ID. Check the series metadata in Jellyfin/Emby.");
            if (!season.HasValue || !episode.HasValue) return Empty("Search skipped: season or episode number is missing.");

            var mappings = await GetJsonAsync<Dictionary<string, MediaMapping>>(MappingUrl, false, cancellationToken).ConfigureAwait(false);
            var match = SelectMapping(mappings?.Values ?? Enumerable.Empty<MediaMapping>(), tvdb, season.Value, episode.Value);
            diagnostic($"Loaded {mappings?.Count ?? 0} anime mappings; {mappings?.Values.Count(m => m != null && m.tvdb_id == tvdb) ?? 0} match series TVDB {tvdb}.");
            if (match == null) return Empty($"No AniList mapping for TVDB {tvdb}, season {season}, episode {episode}.");
            diagnostic($"Mapped TVDB {tvdb} S{season}E{episode} to AniList {match.anilist_id}, episode {episode.Value - match.tvdb_epoffset} (offset {match.tvdb_epoffset}).");

            var entries = await GetJsonAsync<List<JimakuSearch>>(ApiRoot + "entries/search?anilist_id=" + match.anilist_id, true, cancellationToken).ConfigureAwait(false);
            diagnostic($"Jimaku returned {entries?.Count ?? 0} entries for AniList {match.anilist_id}.");
            var files = new List<JimakuFile>();
            foreach (var entry in entries ?? new List<JimakuSearch>())
            {
                var found = await GetJsonAsync<List<JimakuFile>>(ApiRoot + "entries/" + entry.Id + "/files?episode=" + (episode.Value - match.tvdb_epoffset), true, cancellationToken).ConfigureAwait(false);
                diagnostic($"Jimaku entry {entry.Id} returned {found?.Count ?? 0} files.");
                files.AddRange(found ?? new List<JimakuFile>());
            }
            var result = files.Where(f => f != null && Uri.TryCreate(f.Url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                .GroupBy(f => f.Url).Select(g => g.First()).ToList();
            diagnostic($"Returning {result.Count} subtitle files from {files.Count} results after URL validation and deduplication.");
            return result;
        }

        private IReadOnlyList<JimakuFile> Empty(string reason)
        {
            diagnostic(reason);
            return Array.Empty<JimakuFile>();
        }

        public static MediaMapping SelectMapping(IEnumerable<MediaMapping> mappings, int tvdbId, int season, int episode)
        {
            return mappings.Where(m => m != null && m.tvdb_id == tvdbId && m.anilist_id > 0 &&
                    (m.tvdb_season == season || m.tvdb_season == -1) && episode > m.tvdb_epoffset)
                .OrderByDescending(m => m.tvdb_season == season)
                .ThenByDescending(m => m.tvdb_epoffset).FirstOrDefault();
        }

        public string EncodeId(string url) => Convert.ToBase64String(Encoding.Unicode.GetBytes(json.Serialize(url)));
        // Retains IDs returned by older Emby versions.
        public string DecodeId(string id) => json.Deserialize<string>(Encoding.Unicode.GetString(Convert.FromBase64String(id.Replace(" ", string.Empty))));
        public static string GetFormat(string url) => Path.GetExtension(new Uri(url).AbsolutePath).TrimStart('.').ToLowerInvariant();

        public async Task<Stream> DownloadAsync(string url, CancellationToken cancellationToken)
        {
            var uri = new Uri(url, UriKind.Absolute);
            if (uri.Scheme != "https") throw new ArgumentException("Subtitle URLs must use HTTPS.", nameof(url));
            // Public file URLs need no API credentials, including when hosted elsewhere.
            using (var request = CreateRequest(url, false))
            using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                diagnostic($"Subtitle download HTTP {(int)response.StatusCode}.");
                response.EnsureSuccessStatusCode();
                using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    var output = new MemoryStream();
                    try
                    {
                        await source.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
                        output.Position = 0;
                        return output;
                    }
                    catch { output.Dispose(); throw; }
                }
            }
        }

        private HttpRequestMessage CreateRequest(string url, bool authenticated)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var version = typeof(JimakuClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "unknown";
            request.Headers.UserAgent.ParseAdd("Jimaku.Subtitles/" + version);
            if (authenticated) request.Headers.TryAddWithoutValidation("Authorization", apiKey());
            return request;
        }

        private async Task<T> GetJsonAsync<T>(string url, bool authenticated, CancellationToken cancellationToken)
        {
            diagnostic($"GET {url}");
            using (var request = CreateRequest(url, authenticated))
            using (var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                diagnostic($"HTTP {(int)response.StatusCode} for {url}");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return json.Deserialize<T>(content);
            }
        }
    }
}
