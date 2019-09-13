using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Roki.Core.Services;
using Roki.Extensions;

namespace Roki.Modules.Searches.Services
{
    public class AnimeService : IRService
    {
        private string _queryJson =
            @"{""query"":""query ($search: String) { Page(page:1 perPage:5) { media(search: $search) { title { romaji english native } description averageScore status episodes genres type seasonInt coverImage { large color } } } }"", ""variables"": { ""search"": ""searchString"" } }";
        private readonly IHttpClientFactory _httpFactory;

        public AnimeService(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
        }

        public async Task<List<Anime>> GetAnimeDataAsync(string query)
        {
            try
            {
                query = query.SanitizeString();
                using (var http = _httpFactory.CreateClient())
                {
                    var newQuery = _queryJson.Replace("searchString", query);
                    var content = new StringContent(newQuery, Encoding.UTF8, "application/json");
                    var result = await http.PostAsync("https://graphql.anilist.co", content).ConfigureAwait(false);
                    var model = JsonConvert.DeserializeObject<AnimeModel>(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                    var media = model.Data.Page.Media;
                    return media;
                }
            }
            catch
            {
                return null;
            }
        }
    }

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
        public int SeasonInt { get; set; }
        public List<string> Genres { get; set; }
        public int Episodes { get; set; }
        public int AverageScore { get; set; }
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