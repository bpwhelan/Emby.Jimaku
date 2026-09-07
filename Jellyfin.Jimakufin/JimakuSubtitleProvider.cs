using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jimaku.Shared;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Jimakufin
{
    public class JimakuSubtitleProvider : ISubtitleProvider
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILibraryManager library;
        public JimakuSubtitleProvider(IHttpClientFactory httpClientFactory, ILibraryManager library)
        {
            this.httpClientFactory = httpClientFactory;
            this.library = library;
        }
        public string Name => "Jimakufin";
        public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Episode };

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.ContentType != VideoContentType.Episode || string.IsNullOrEmpty(request.MediaPath) ||
                !JimakuClient.IsJapanese(request.Language)) return Array.Empty<RemoteSubtitleInfo>();
            // Request.ProviderIds contains episode IDs, not the series TVDB ID.
            var episode = library.GetItemList(new InternalItemsQuery { Path = request.MediaPath, Limit = 1 }).OfType<Episode>().FirstOrDefault();
            var tvdb = episode?.Series?.ProviderIds?.FirstOrDefault(p => string.Equals(p.Key, "Tvdb", StringComparison.OrdinalIgnoreCase)).Value;
            using (var http = httpClientFactory.CreateClient())
            {
                var client = CreateClient(http);
                var files = await client.SearchAsync(tvdb, request.ParentIndexNumber, request.IndexNumber, request.Language, cancellationToken).ConfigureAwait(false);
                return files.Select(file => new RemoteSubtitleInfo
                {
                    Id = client.EncodeId(file.Url), Name = file.Name, ProviderName = Name,
                    ThreeLetterISOLanguageName = "jpn", Format = JimakuClient.GetFormat(file.Url)
                }).ToList();
            }
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            using (var http = httpClientFactory.CreateClient())
            {
                var client = CreateClient(http);
                var url = client.DecodeId(id);
                return new SubtitleResponse
                {
                    Format = JimakuClient.GetFormat(url), Language = "jpn",
                    Stream = await client.DownloadAsync(url, cancellationToken).ConfigureAwait(false)
                };
            }
        }
        private static JimakuClient CreateClient(HttpClient http) => new JimakuClient(http, new JsonCodec(), () => Plugin.Instance?.Configuration.ApiKey);
        private sealed class JsonCodec : IJsonCodec
        {
            private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            public T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Options);
            public string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
        }
    }
}

