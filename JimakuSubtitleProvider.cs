using Emby.Jimaku.Model;
using EmbyPluginUiDemo;
using EmbyPluginUiDemo.Jimaku;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Jimaku
{
    internal class JimakuSubtitleProvider : ISubtitleProvider, IHasOrder
    {
        private readonly IHttpClient httpClient;
        private readonly IFileSystem fileSystem;
        private readonly IApplicationPaths appPaths;
        private readonly IJsonSerializer json;
        private readonly ILogger logger;
        private readonly JimakuApiClient jimakuApiClient;

        public string Name => Plugin.PluginName;

        public IEnumerable<VideoContentType> SupportedMediaTypes => new List<VideoContentType> { VideoContentType.Episode };

        public int Order => 1;

        public JimakuSubtitleProvider(IHttpClient httpClient, IFileSystem fileSystem, IApplicationPaths appPaths, IJsonSerializer json, ILogger logger)
        {
            this.httpClient = httpClient;
            this.fileSystem = fileSystem;
            this.appPaths = appPaths;
            this.json = json;
            this.logger = logger;
            this.jimakuApiClient = new JimakuApiClient(httpClient, json, logger);
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            byte[] decodedBytes = Convert.FromBase64String(id);
            var str = Encoding.Unicode.GetString(decodedBytes);
            var file = json.DeserializeFromString<JimakuFile>(str);

            return await jimakuApiClient.DownloadFileAsync(file);
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            var result = new List<RemoteSubtitleInfo>();

            logger.Info(json.SerializeToString(request));
            var search = await jimakuApiClient.SearchByAnilistID(request.SeriesProviderIds["AniList"]);

            var files = await jimakuApiClient.GetFilesFromSearch(search[0], request.IndexNumber.Value);

            foreach (var file in files)
            {
                var fileExtension = Path.GetExtension(file.Url).TrimStart('.'); // Removes the '.' from extension
                result.Add(new RemoteSubtitleInfo
                {
                    Id = Convert.ToBase64String(Encoding.Unicode.GetBytes(json.SerializeToString(file))),
                    Name = file.Name,
                    ProviderName = Plugin.PluginName,
                    Language = "jpn",
                    Format = fileExtension
                });
            }


            logger.Info(json.SerializeToString(result));

            return result;
        }
    }
}
