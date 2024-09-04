using MediaBrowser.Common.Net;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;
using Emby.Jimaku.Model;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Controller.Entities.TV;
using System.IO.Pipes;
using MediaBrowser.Common.Configuration;
using Emby.Jimaku;
using MediaBrowser.Common;

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
            // Set up the request
            var request = new HttpRequestOptions
            {
                Url = $"https://jimaku.cc/api/entries/search?anilist_id={anilist_id}"
            };

            // Add the Authorization header
            request.RequestHeaders.Add("Authorization", config.ApiKey);


            try
            {
                // Send the request and get the response
                var response = await httpClient.SendAsync(request, HttpMethod.Get.ToString());

                // Read the content as a string
                var responseBody = response.Content;


                var search = json.DeserializeFromStream<List<JimakuSearch>>(responseBody);

                logger.Info(json.SerializeToString(search));

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
            // Set up the request
            var request = new HttpRequestOptions
            {
                Url = $"https://jimaku.cc/api/entries/{search.Id}/files?episode={episode}"
            };

            // Add the Authorization header
            request.RequestHeaders.Add("Authorization", config.ApiKey);


            try
            {
                // Send the request and get the response
                var response = await httpClient.SendAsync(request, HttpMethod.Get.ToString());

                // Read the content as a string
                var responseBody = response.Content;


                var files = json.DeserializeFromStream<List<JimakuFile>>(responseBody);

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
                var request = new HttpRequestOptions
                {
                    Url = file.Url
                };

                logger.Info(json.SerializeToString(request));

                var response = await httpClient.SendAsync(request, HttpMethod.Get.ToString());

                // Get the file stream from the response
                var fileStream = response.Content;

                // Extract the file extension from the URL (assuming the file extension is in the URL)
                var fileExtension = Path.GetExtension(file.Url).TrimStart('.'); // Removes the '.' from extension

                MemoryStream fileData = new MemoryStream();
                await response.Content.CopyToAsync(fileData);
                fileData.Position = 0L;

                logger.Info($"Downloading {fileExtension}");

                logger.Info(response.Content.ToString());

                // Return the file stream and the file extension as the file type
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
    }
}
