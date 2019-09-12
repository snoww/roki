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

        public async Task<dynamic> GetAnimeDataAsync(string query)
        {
            try
            {
                query = query.SanitizeString();
                using (var http = _httpFactory.CreateClient())
                {
                    _queryJson = _queryJson.Replace("searchString", query);
                    var content = new StringContent(_queryJson, Encoding.UTF8, "application/json");
                    var result = await http.PostAsync("https://graphql.anilist.co", content).ConfigureAwait(false);
                    dynamic json = JsonConvert.DeserializeObject(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                    var media = json.data["Page"].media;
                    return media;
                }
            }
            catch
            {
                return null;
            }
            
        }
        
    }
}