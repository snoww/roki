using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Roki.Core.Services;

namespace Roki.Modules.Searches.Services
{
    public class AnimeService : IRService
    {
        private string queryJson =
            @"{""query"":""query ($search: String) { Page(page:1 perPage:5) { media(search: $search) { title { romaji english native } description averageScore status episodes genres type seasonInt coverImage { large color } } } }"", ""variables"": { ""search"": searchstring } }";
        private readonly IHttpClientFactory _httpFactory;

        public AnimeService(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
        }

        public async Task<AnimeData> GetAnimeDataAsync(string query)
        {
            using (var http = _httpFactory.CreateClient())
            {
                var content = new StringContent(queryJson, Encoding.UTF8, "application/json");
                var result = await http.PostAsync("https://graphql.anilist.co", content).ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<AnimeData>(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
        }
        
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