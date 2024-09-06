using System;
using System.Collections.Generic;
using System.Text;

namespace Emby.Jimaku.Model
{
    public class MediaMapping
    {
        public int? tvdb_id { get; set; }
        public int tvdb_season { get; set; }
        public int tvdb_epoffset { get; set; }
        public int? mal_id { get; set; }
        public int? anilist_id { get; set; }
        public string imdb_id { get; set; }
        public int? tmdb_show_id { get; set; }
        public int? tmdb_movie_id { get; set; }
    }
}
