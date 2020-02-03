using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Roki.Extensions;
using Roki.Modules.Searches.Models;
using Roki.Services;

namespace Roki.Modules.Searches.Services
{
    public class AnimeService : IRokiService
    {
        private const string QueryJson = @"{""query"":""query ($search: String) { Page(page:1 perPage:5) { media(search: $search) { title { romaji english native } description averageScore status episodes genres type seasonInt coverImage { large color } } } }"", ""variables"": { ""search"": ""searchString"" } }";

        private readonly IHttpClientFactory _httpFactory;

        public AnimeService(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
        }

        public async Task<List<Anime>> GetAnimeDataAsync(string query)
        {
            try
            {
                query = query.SanitizeStringFull();
                using var http = _httpFactory.CreateClient();
                var newQuery = QueryJson.Replace("searchString", query);
                var content = new StringContent(newQuery, Encoding.UTF8, "application/json");
                var result = await http.PostAsync("https://graphql.anilist.co", content).ConfigureAwait(false);
                var model = (await result.Content.ReadAsStringAsync().ConfigureAwait(false)).Deserialize<AnimeModel>();
                var media = model.Data.Page.Media;
                return media;
            }
            catch
            {
                return null;
            }
        }
    }
}