using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Roki.Core.Services;
using Roki.Modules.Searches.Models;

namespace Roki.Modules.Searches.Services
{
    public class WikipediaService : IRokiService
    {
        private readonly IHttpClientFactory _http;

        // allow other languages in future
        private const string ApiUrl = "https://en.wikipedia.org/w/api.php?action=query";
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions{PropertyNameCaseInsensitive = true};

        public WikipediaService(IHttpClientFactory http)
        {
            _http = http;
        }

        public async Task<List<WikiSearch>> SearchAsync(string query, int limit = 5)
        {
            using var http = _http.CreateClient();
            var result = await http.GetStringAsync($"{ApiUrl}&list=search&srsearch={query}&srlimit={limit}&format=json").ConfigureAwait(false);

            using var json = JsonDocument.Parse(result);
            var queryElement = json.RootElement.GetProperty("query");

            var searchInfo = queryElement.GetProperty("searchinfo");
            var hits = searchInfo.GetProperty("totalhits").GetInt32();

            var results = new List<WikiSearch>();

            if (hits != 0)
            {
                var enumerator = queryElement.GetProperty("search").EnumerateArray();
            
                foreach (var page in enumerator)
                {
                    var snippet = page.GetProperty("snippet").GetString().Replace("<span class=\\\"searchmatch\\\">", "**").Replace("</span>", "**");
                    results.Add(new WikiSearch
                    {
                        Title = page.GetProperty("title").GetString(),
                        Snippet = HttpUtility.HtmlDecode(snippet)
                    });
                }
            }
            else if (searchInfo.TryGetProperty("suggestion", out var suggestion))
            {
                results.Add(new WikiSearch
                {
                    Title = suggestion.GetString()
                });
            }

            return results;
        }

        public async Task<WikiSummary> GetSummaryAsync(string title)
        {
            using var http = _http.CreateClient();
            var result = await http.GetStringAsync($"{ApiUrl}&list=&prop=extracts&explaintext&exsentences=5&exintro=&titles={title}&format=json").ConfigureAwait(false);

            var summary = new WikiSummary();
            using (var json = JsonDocument.Parse(result))
            {
                var element = json.RootElement.GetProperty("query").GetProperty("pages");
                var page = element.EnumerateObject().First();
                summary.Title = page.Value.GetProperty("title").GetString();
                summary.Extract = page.Value.GetProperty("extract").GetString();
            }
            
            var imageResult = await http.GetStringAsync($"{ApiUrl}&titles={title}&prop=pageimages&pithumbsize=500&format=json").ConfigureAwait(false);

            using (var json = JsonDocument.Parse(imageResult))
            {
                var element = json.RootElement.GetProperty("query").GetProperty("pages");
                var page = element.EnumerateObject().First();
                if (page.Value.TryGetProperty("thumbnail", out var thumbnail))
                {
                    summary.ImageUrl = thumbnail.GetProperty("source").GetString();
                }
            }
            
            return summary;
        }
    }
}