using System;
using System.Collections.Generic;
using System.Text;

namespace Emby.Jimaku.Model
{
    // Only deserialize fields used for TVDB-to-AniList matching. Other IDs can
    // contain comma-separated lists (e.g. mal_id="849,4382"), not integers.
    public class MediaMapping
    {
        public int? tvdb_id { get; set; }
        public int? tvdb_season { get; set; }
        public int tvdb_epoffset { get; set; }
        public int? anilist_id { get; set; }
    }
}
