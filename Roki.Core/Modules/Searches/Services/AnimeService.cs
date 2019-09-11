using System.Collections.Generic;
using Roki.Core.Services;

namespace Roki.Modules.Searches.Services
{
    public class AnimeService : IRService
    {
        
    }

    public class AnimeData
    {
        public Dictionary<string, AnimePage> Data { get; set; }
    }
    
    public class AnimePage
    {
        public Dictionary<string, AnimeMedia> Page { get; set; }
    }
    
    public class AnimeMedia
    {
        public Dictionary<string, AnimeModel> Media { get; set; }
    }

    public class AnimeModel
    {
        public AnimeTitle Title { get; set; }
        public string Description { get; set; }
        public int AverageScore { get; set; }
        public string Status { get; set; }
        public int Episodes { get; set; }
        public string[] Genres { get; set; }
        public string Type { get; set; }
        public int SeasonInt { get; set; }
        public CoverImage CoverImage { get; set; }
    }

    public class AnimeTitle
    {
        public string Romaji { get; set; }
        public string English { get; set; }
        public string Native { get; set; }
    }

    public class CoverImage
    {
        public string Large { get; set; }
        public string Color { get; set; }
    }
}