namespace Emby.Jimaku.Model
{
    public class TVDBToAnilistMapping
    {
        public string Title { get; set; }
        public int AniListID { get; set; }
        public int TVDBID { get; set; }
        public int? Season { get; set; } // Nullable in case some entries don't have a season
    }
}
