using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jimaku.Shared;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Jimakufin
{
    public class JimakuSubtitleProvider : ISubtitleProvider
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILibraryManager library;
        private readonly ILogger<JimakuSubtitleProvider> logger;
        public JimakuSubtitleProvider(IHttpClientFactory httpClientFactory, ILibraryManager library, ILogger<JimakuSubtitleProvider> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.library = library;
            this.logger = logger;
            logger.LogInformation("Jimakufin subtitle provider initialized. Plugin version {Version}; API key configured: {HasApiKey}", typeof(Plugin).Assembly.GetName().Version, !string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration.ApiKey));
        }
        public string Name => "Jimakufin";
        public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Episode };

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Jimakufin search: series {Series}, season {Season}, episode {Episode}, type {ContentType}, language {Language}, two-letter language {TwoLetterLanguage}, API key configured: {HasApiKey}",
                request.SeriesName, request.ParentIndexNumber, request.IndexNumber, request.ContentType, request.Language, request.TwoLetterISOLanguageName, !string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration.ApiKey));
            logger.LogDebug("Jimakufin search media path: {MediaPath}", request.MediaPath);
            try
            {
                return await SearchInternal(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Jimakufin search cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError("Jimakufin search failed ({ErrorType}). Check the preceding lookup and HTTP status messages.", ex.GetType().Name);
                throw;
            }
        }

        private async Task<IEnumerable<RemoteSubtitleInfo>> SearchInternal(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.ContentType != VideoContentType.Episode || string.IsNullOrEmpty(request.MediaPath) ||
                !JimakuClient.IsJapanese(request.Language))
            {
                logger.LogInformation("Jimakufin search skipped: requires an episode, a media path, and Japanese language. Type={Type}, has path={HasPath}, language={Language}", request.ContentType, !string.IsNullOrEmpty(request.MediaPath), request.Language);
                return Array.Empty<RemoteSubtitleInfo>();
            }
            // Request.ProviderIds contains episode IDs, not the series TVDB ID.
            var episode = library.GetItemList(new InternalItemsQuery { Path = request.MediaPath, IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Episode }, Limit = 1 }).OfType<Episode>().FirstOrDefault();
            if (episode == null)
            {
                logger.LogWarning("Jimakufin could not resolve the library episode from its media path; no series metadata is available.");
                return Array.Empty<RemoteSubtitleInfo>();
            }
            var tvdb = episode?.Series?.ProviderIds?.FirstOrDefault(p => string.Equals(p.Key, "Tvdb", StringComparison.OrdinalIgnoreCase)).Value;
            logger.LogInformation("Jimakufin resolved episode {EpisodeId}, series {SeriesId}; series TVDB ID={TvdbId}; available series provider keys={ProviderKeys}", episode.Id, episode.SeriesId, tvdb ?? "missing", string.Join(", ", episode.Series?.ProviderIds?.Keys ?? Enumerable.Empty<string>()));
            if (string.IsNullOrWhiteSpace(tvdb))
                logger.LogWarning("Jimakufin requires a TVDB ID on the parent series, not the episode. Edit the series metadata and populate its TheTVDB ID. TMDB-only series cannot currently be matched.");
            using (var http = httpClientFactory.CreateClient())
            {
                var client = CreateClient(http);
                var files = await client.SearchAsync(tvdb, request.ParentIndexNumber ?? episode.ParentIndexNumber, request.IndexNumber ?? episode.IndexNumber, request.Language, cancellationToken).ConfigureAwait(false);
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
        private JimakuClient CreateClient(HttpClient http) => new JimakuClient(http, new JsonCodec(), () => Plugin.Instance?.Configuration.ApiKey, message => logger.LogInformation("Jimakufin: {Diagnostic}", message));
    }
}

