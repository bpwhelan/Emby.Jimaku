using Jimaku.Shared;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Logging;
using System;
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
        private readonly ILogger logger;
        public JimakuSubtitleProvider(IJsonSerializer json, ILogManager logManager)
        {
            logger = logManager.GetLogger("Jimakufin");
            logger.Info("Jimakufin Emby subtitle provider initialized; API key configured: {0}", !string.IsNullOrWhiteSpace(Plugin.Options?.ApiKey));
            client = new JimakuClient(Http, new JsonCodec(json), () => Plugin.Options?.ApiKey, message => logger.Info("Jimakufin: {0}", message));
        }
        public string Name => Plugin.PluginName;
        public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Episode };
        public int Order => 1;

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            logger.Info("Jimakufin Emby search: season {0}, episode {1}, language {2}, API key configured: {3}", request.ParentIndexNumber, request.IndexNumber, request.Language, !string.IsNullOrWhiteSpace(Plugin.Options?.ApiKey));
            try
            {
                var tvdb = request.SeriesProviderIds?.FirstOrDefault(p => string.Equals(p.Key, "Tvdb", StringComparison.OrdinalIgnoreCase)).Value;
                logger.Info("Jimakufin Emby series TVDB ID: {0}", tvdb ?? "missing");
                var files = await client.SearchAsync(tvdb, request.ParentIndexNumber, request.IndexNumber, request.Language, cancellationToken).ConfigureAwait(false);
                return files.Select(file => new RemoteSubtitleInfo
                {
                    Id = client.EncodeId(file.Url), Name = file.Name, ProviderName = Name,
                    Language = "jpn", Format = JimakuClient.GetFormat(file.Url)
                }).ToList();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.Info("Jimakufin Emby search cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                logger.Error("Jimakufin Emby search failed ({0}). Check preceding lookup and HTTP status messages.", ex.GetType().Name);
                throw;
            }
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
