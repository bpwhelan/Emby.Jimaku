using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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

        public JimakuClient(HttpClient http, IJsonCodec json, Func<string> apiKey)
        {
            this.http = http;
            this.json = json;
            this.apiKey = apiKey;
        }

        public static bool IsJapanese(string language) => string.IsNullOrWhiteSpace(language) ||
            string.Equals(language, "ja", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "jpn", StringComparison.OrdinalIgnoreCase);

        public async Task<IReadOnlyList<JimakuFile>> SearchAsync(string tvdbId, int? season, int? episode, string language, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsJapanese(language) || string.IsNullOrWhiteSpace(apiKey()) ||
                !int.TryParse(tvdbId, out var tvdb) || !season.HasValue || !episode.HasValue)
                return Array.Empty<JimakuFile>();

            var mappings = await GetJsonAsync<Dictionary<string, MediaMapping>>(MappingUrl, false, cancellationToken).ConfigureAwait(false);
            var match = SelectMapping(mappings?.Values ?? Enumerable.Empty<MediaMapping>(), tvdb, season.Value, episode.Value);
            if (match == null) return Array.Empty<JimakuFile>();

            var entries = await GetJsonAsync<List<JimakuSearch>>(ApiRoot + "entries/search?anilist_id=" + match.anilist_id, true, cancellationToken).ConfigureAwait(false);
            var files = new List<JimakuFile>();
            foreach (var entry in entries ?? new List<JimakuSearch>())
            {
                var found = await GetJsonAsync<List<JimakuFile>>(ApiRoot + "entries/" + entry.Id + "/files?episode=" + (episode.Value - match.tvdb_epoffset), true, cancellationToken).ConfigureAwait(false);
                files.AddRange(found ?? new List<JimakuFile>());
            }
            return files.Where(f => f != null && Uri.TryCreate(f.Url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                .GroupBy(f => f.Url).Select(g => g.First()).ToList();
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
            request.Headers.UserAgent.ParseAdd("Jimaku.Subtitles/1.0.1");
            if (authenticated) request.Headers.TryAddWithoutValidation("Authorization", apiKey());
            return request;
        }

        private async Task<T> GetJsonAsync<T>(string url, bool authenticated, CancellationToken cancellationToken)
        {
            using (var request = CreateRequest(url, authenticated))
            using (var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return json.Deserialize<T>(content);
            }
        }
    }
}
