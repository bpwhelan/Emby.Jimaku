using Emby.Jimaku;
using Emby.Jimaku.Model;
using EmbyPluginUiDemo.Jimaku;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace EmbyPluginUiDemo.Jimaku
{
    internal static class TVDBToAnilistHelper
    {

        public static async Task<String> GetAnilistIdFromSeason(String TVDBID, int? season)
        {
            List<TVDBToAnilistMapping> mappings = await GetMappingsFromCsvAsync("https://raw.githubusercontent.com/noggl/AniListToTVDB/main/mapping.csv");
            foreach (var mapping in mappings)
            {
                if (mapping.Season == season && mapping.TVDBID.ToString() == TVDBID)
                {
                    return mapping.AniListID.ToString();
                }
            }
            return null;
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