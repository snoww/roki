using System.Collections.Generic;

namespace Roki.Modules.Searches.Models
{
    public class AnimeModel
    {
        public AnimeData Data { get; set; }

    }
    public class AnimeData
    {
        public AnimePage Page { get; set; }
    }

    public class AnimePage
    {
        public List<Anime> Media { get; set; }
    }
    
    public class Anime
    {
        public AnimeTitle Title { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public int? SeasonInt { get; set; }
        public List<string> Genres { get; set; }
        public int? Episodes { get; set; }
        public int? AverageScore { get; set; }
        public AnimeCover CoverImage { get; set; }
    }
    
    public class AnimeTitle
    {
        public string Romaji { get; set; }
        public string English { get; set; }
        public string Native { get; set; }
    }

    public class AnimeCover
    {
        public string Large { get; set; }
        public string Color { get; set; }
    }
}