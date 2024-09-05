
using Emby.Jimaku;
using Emby.Jimaku.Model;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace EmbyPluginUiDemo.Jimaku
{
    internal class JimakuApiClient
    {
        private readonly IHttpClient httpClient;
        private readonly IJsonSerializer json;
        private readonly ILogger logger;
        private readonly PluginOptions config;

        public JimakuApiClient(IHttpClient httpClient, IJsonSerializer jsonSerializer, ILogger logger)
        {
            this.httpClient = httpClient;
            this.json = jsonSerializer;
            this.logger = logger;
            this.config = Plugin.Options;
            
        }

        public async Task<List<JimakuSearch>> SearchByAnilistID(String anilist_id)
        {
            HttpRequestOptions requestOptions = GetRequestOptions();
            requestOptions.Url = $"https://jimaku.cc/api/entries/search?anilist_id={anilist_id}";

            try
            {
                var response = await httpClient.SendAsync(requestOptions, HttpMethod.Get.ToString());
                var search = json.DeserializeFromStream<List<JimakuSearch>>(response.Content);

                return search;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
                return null;
            }
        }

        public async Task<List<JimakuSearch>> SearchByTVDB_ID(String tvdb_id, int? season)
        {
            HttpRequestOptions requestOptions = GetRequestOptions();
            var anilist_id = await TVDBToAnilistHelper.GetAnilistIdFromSeason(tvdb_id, season);
            requestOptions.Url = $"https://jimaku.cc/api/entries/search?anilist_id={anilist_id}";

            try
            {
                var response = await httpClient.SendAsync(requestOptions, HttpMethod.Get.ToString());
                var search = json.DeserializeFromStream<List<JimakuSearch>>(response.Content);

                return search;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
                return null;
            }
        }

        public async Task<List<JimakuFile>> GetFilesFromSearch(JimakuSearch search, int episode)
        {
            HttpRequestOptions requestOptions = GetRequestOptions();
            requestOptions.Url = $"https://jimaku.cc/api/entries/{search.Id}/files?episode={episode}";

            try
            {
                var response = await httpClient.SendAsync(requestOptions, HttpMethod.Get.ToString());

                var files = json.DeserializeFromStream<List<JimakuFile>>(response.Content);

                logger.Info(json.SerializeToString(files));

                return files;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
                return null;
            }
        }

        public async Task<SubtitleResponse> DownloadFileAsync(JimakuFile file)
        {
            logger.Info(json.SerializeToString(file));
            try
            {
                HttpRequestOptions requestOptions = GetRequestOptions();
                requestOptions.Url = file.Url;

                var response = await httpClient.SendAsync(requestOptions, HttpMethod.Get.ToString());

                // Extract the file extension from the URL (assuming the file extension is in the URL)
                var fileExtension = Path.GetExtension(file.Url).TrimStart('.'); // Removes the '.' from extension

                MemoryStream fileData = new MemoryStream();
                await response.Content.CopyToAsync(fileData);
                fileData.Position = 0L;

                logger.Info($"Downloading {fileExtension}");

                return new SubtitleResponse
                {
                    Format = fileExtension,
                    Stream = fileData,
                    Language = "jpn"
                };
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Download error: {e.Message}");
                return new SubtitleResponse
                {
                    Format = null,
                    Stream = null,
                    Language = "jpn"
                };
            }
        }

        private HttpRequestOptions GetRequestOptions()
        {
            HttpRequestOptions requestOptions = new HttpRequestOptions();
            var infoVersion = Assembly.GetExecutingAssembly()?
              .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
              .InformationalVersion;
            requestOptions.UserAgent = $"Emby.Jimaku.{infoVersion}";
            requestOptions.RequestHeaders.Add("Authorization", config.ApiKey);
            return requestOptions;
        }
    }
}
