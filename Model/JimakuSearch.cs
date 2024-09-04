using System;
using System.Collections.Generic;


namespace Emby.Jimaku.Model
{
    public class JimakuSearch
    {
        public long Id { get; set; } // ID of the entry
        public string Name { get; set; } // Romaji name of the entry
        public Flags Flags { get; set; } // Flags associated with the entry
        public DateTime LastModified { get; set; } // Date of the newest uploaded file as RFC3339 timestamp
        public int? AnilistId { get; set; } // Anilist ID of this entry (nullable)
        public long? CreatorId { get; set; } // Account ID that created this entry (nullable)
        public string EnglishName { get; set; } // English name of the entry (nullable)
        public string JapaneseName { get; set; } // Japanese name of the entry (nullable)
        public string Notes { get; set; } // Extra notes (nullable)
        public string TmdbId { get; set; } // TMDB ID of the entry (nullable)
    }

    public class Flags
    {
        public bool Adult { get; set; } // Entry is for adult audiences
        public bool Anime { get; set; } // Entry is for an anime
        public bool External { get; set; } // Entry comes from an external source
        public bool Movie { get; set; } // Entry is a movie
        public bool Unverified { get; set; } // Entry is unverified
    }

    // Example of usage:
    public class EntryList
    {
        public List<JimakuSearch> Entries { get; set; }
    }
}
