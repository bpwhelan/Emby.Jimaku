using Emby.Jimaku.Model;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace EmbyPluginUiDemo.Jimaku
{
    internal static class TVDBToAnilistHelper
    {

        public static async Task<List<String>> GetAnilistIdFromSeason(SubtitleSearchRequest request, int? season, IJsonSerializer json, IHttpClient httpClient)
        {
            List<MediaMapping> mappings = await GetMappingsFromJsonAsync("https://raw.githubusercontent.com/Kometa-Team/Anime-IDs/master/anime_ids.json", json, httpClient);
            List<MediaMapping> tvdbMatched = new List<MediaMapping>();
            foreach (var mapping in mappings)
            {
                if (mapping.tvdb_id.ToString() == request.SeriesProviderIds["TVDB"])
                {
                    tvdbMatched.Add(mapping);
                }
            }

            List<String> ret = new List<String>();
            
            foreach (var match in tvdbMatched)
            {   
                if (match.tvdb_season == request.ParentIndexNumber)
                {
                    if (match.tvdb_epoffset != 0 && request.IndexNumber > match.tvdb_epoffset)
                    {
                        ret = new List<String> { match.anilist_id.ToString() };
                        break;
                    } else if (match.tvdb_epoffset == 0)
                    {
                        ret = new List<String> { match.anilist_id.ToString() };
                    }
                }
            }
            return ret;
        }

        public static async Task<List<MediaMapping>> GetMappingsFromJsonAsync(string url, IJsonSerializer json, IHttpClient httpClient)
        {
            var httpOptions = new HttpRequestOptions{ Url = url };
            var response = await httpClient.Get(httpOptions);
            return new List<MediaMapping>(json.DeserializeFromStream<Dictionary<string, MediaMapping>>(response).Values);
        }

        public static async Task<List<TVDBToAnilistMapping>> GetMappingsFromCsvAsync(string url)
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetStringAsync(url);
                return ParseCsv(response);
            }
        }

        public static List<TVDBToAnilistMapping> ParseCsv(string csvContent)
        {
            var mappings = new List<TVDBToAnilistMapping>();
            using (var reader = new StringReader(csvContent))
            {
                string line;
                bool isFirstLine = true;

                while ((line = reader.ReadLine()) != null)
                {
                    // Skip header line
                    if (isFirstLine)
                    {
                        isFirstLine = false;
                        continue;
                    }

                    // Split the line by comma (assuming simple CSV with no escaped commas)
                    var fields = TVDBToAnilistHelper.ReverseSplit(line, ';', 4);


                    if (fields.Length >= 4)
                    {
                        string title = fields[0];
                        int aniListID = int.Parse(fields[1]);
                        int tvdbID = int.Parse(fields[2]);

                        // Handle nullable Season field
                        int? season = string.IsNullOrWhiteSpace(fields[3]) ? (int?)null : int.Parse(fields[3]);

                        var mapping = new TVDBToAnilistMapping
                        {
                            Title = title,
                            AniListID = aniListID,
                            TVDBID = tvdbID,
                            Season = season
                        };

                        mappings.Add(mapping);
                    }
                }
            }

            return mappings;
        }

        // Helper method to reverse split
        private static string[] ReverseSplit(string input, char separator, int limit)
        {
            // Split the input normally
            var parts = input.Split(separator);

            // Reverse the split array and take the last 'limit - 1' elements for the final fields
            var reverseParts = parts.Reverse().Take(limit - 1).Reverse().ToArray();

            // Take the remaining part as the title and join it
            var title = string.Join(separator.ToString(), parts.Take(parts.Length - (limit - 1)));

            // Combine the title and reverse parts back into an array
            return new[] { title }.Concat(reverseParts).ToArray();
        }
    }
}