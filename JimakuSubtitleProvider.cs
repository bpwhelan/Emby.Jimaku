using Jimaku.Shared;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Jimaku
{
    public class JimakuSubtitleProvider : ISubtitleProvider, IHasOrder
    {
        private static readonly HttpClient Http = new HttpClient();
        private readonly JimakuClient client;
        public JimakuSubtitleProvider(IJsonSerializer json)
        {
            client = new JimakuClient(Http, new JsonCodec(json), () => Plugin.Options.ApiKey);
        }
        public string Name => Plugin.PluginName;
        public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Episode };
        public int Order => 1;

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            var tvdb = request.SeriesProviderIds?.FirstOrDefault(p => string.Equals(p.Key, "Tvdb", System.StringComparison.OrdinalIgnoreCase)).Value;
            var files = await client.SearchAsync(tvdb, request.ParentIndexNumber, request.IndexNumber, request.Language, cancellationToken).ConfigureAwait(false);
            return files.Select(file => new RemoteSubtitleInfo
            {
                Id = client.EncodeId(file.Url), Name = file.Name, ProviderName = Name,
                Language = "jpn", Format = JimakuClient.GetFormat(file.Url)
            }).ToList();
        }
        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            var url = client.DecodeId(id);
            return new SubtitleResponse
            {
                Format = JimakuClient.GetFormat(url), Language = "jpn",
                Stream = await client.DownloadAsync(url, cancellationToken).ConfigureAwait(false)
            };
        }
        private sealed class JsonCodec : IJsonCodec
        {
            private readonly IJsonSerializer json;
            public JsonCodec(IJsonSerializer json) { this.json = json; }
            public T Deserialize<T>(string value) => json.DeserializeFromString<T>(value);
            public string Serialize<T>(T value) => json.SerializeToString(value);
        }
    }
}
