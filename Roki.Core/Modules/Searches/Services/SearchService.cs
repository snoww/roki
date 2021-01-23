using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using NLog;
using Roki.Extensions;
using Roki.Modules.Searches.Models;
using Roki.Services;

namespace Roki.Modules.Searches.Services
{
    public class SearchService : IRokiService
    {
        private const string ApiUrl = "https://en.wikipedia.org/w/api.php?action=query";
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly string TempDir = Path.GetTempPath();
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _http;

        public SearchService(IHttpClientFactory http, IRokiConfig config)
        {
            _http = http;
            _config = config;
        }

        public async Task<string> GetWeatherDataAsync(float lat, float lng)
        {
            try
            {
                using HttpClient http = _http.CreateClient();
                string result = await http.GetStringAsync($"https://wttr.in/{lat},{lng}?0ATQ").ConfigureAwait(false);
                return result;
            }
            catch (Exception e)
            {
                Logger.Warn(e);
                return null;
            }
        }

        public async Task<TimeData> GetTimeDataAsync(string arg)
        {
            try
            {
                using HttpClient http = _http.CreateClient();
                GeolocationResult obj = await GetLocationDataAsync(arg);
                CultureInfo culture = GetLocalCulture(obj.Results[0].AddressComponents);

                long currentSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string timeResult = await http
                    .GetStringAsync(
                        $"https://maps.googleapis.com/maps/api/timezone/json?location={obj.Results[0].Geometry.Location.Lat},{obj.Results[0].Geometry.Location.Lng}&timestamp={currentSeconds}&key={_config.GoogleApi}")
                    .ConfigureAwait(false);
                var timeObj = timeResult.Deserialize<TimeZoneResult>();

                var timeData = new TimeData
                {
                    Address = obj.Results[0].FormattedAddress,
                    Time = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, timeObj.TimeZoneId),
                    TimeZoneName = timeObj.TimeZoneName,
                    Culture = culture
                };

                return timeData;
            }
            catch (Exception e)
            {
                Logger.Warn(e);
                return null;
            }
        }

        private CultureInfo GetLocalCulture(IEnumerable<AddressComponent> components)
        {
            AddressComponent country = components.FirstOrDefault(c => c.Types.Contains("country", StringComparer.Ordinal));
            if (country == null)
            {
                return null;
            }

            try
            {
                using JsonDocument json = JsonDocument.Parse(File.ReadAllText("./data/countries.json"));
                string lang = json.RootElement.GetProperty(country.ShortName).GetProperty("languages")[0].GetString();
                return new CultureInfo(lang!);
            }
            catch (Exception)
            {
                Logger.Warn("Could not find culture for {Culture}", country.LongName);
                return null;
            }
        }

        public async Task<TimeZoneResult> GetLocalDateTime(float lat, float lng)
        {
            using HttpClient http = _http.CreateClient();
            long currentSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string timeResult = await http
                .GetStringAsync(
                    $"https://maps.googleapis.com/maps/api/timezone/json?location={lat},{lng}&timestamp={currentSeconds}&key={_config.GoogleApi}")
                .ConfigureAwait(false);
            return timeResult.Deserialize<TimeZoneResult>();
        }

        public async Task<GeolocationResult> GetLocationDataAsync(string location)
        {
            try
            {
                using HttpClient http = _http.CreateClient();
                string result = await http.GetStringAsync($"https://maps.googleapis.com/maps/api/geocode/json?address={location}&key={_config.GoogleApi}")
                    .ConfigureAwait(false);
                var geo = result.Deserialize<GeolocationResult>();
                if (geo?.Results == null || geo.Results.Length == 0)
                {
                    Logger.Warn("Geocode lookup failed for {lLocation", location);
                    return null;
                }

                return geo;
            }
            catch (Exception e)
            {
                Logger.Warn(e);
                return null;
            }
        }

        public async Task<OmdbMovie> GetMovieDataAsync(string name)
        {
            name = HttpUtility.UrlEncode(name.ToLowerInvariant());
            using HttpClient http = _http.CreateClient();
            string result = await http.GetStringAsync($"http://www.omdbapi.com/?t={name}&apikey={_config.OmdbApi}&y=&plot=full&r=json").ConfigureAwait(false);
            var movie = result.Deserialize<OmdbMovie>();
            return movie?.Title == null ? null : movie;
        }

        public async Task<string> GetRandomCatAsync()
        {
            using HttpClient http = _http.CreateClient();
            string result = await http.GetStringAsync("https://aws.random.cat/meow").ConfigureAwait(false);
            using JsonDocument cat = JsonDocument.Parse(result);
            using var client = new WebClient();
            var uri = new Uri(cat.RootElement.GetProperty("file").GetString()!);
            string path = TempDir + Path.GetFileName(uri.LocalPath);
            await client.DownloadFileTaskAsync(uri, path).ConfigureAwait(false);
            return path;
        }

        public async Task<string> GetRandomDogAsync()
        {
            using HttpClient http = _http.CreateClient();
            string result = await http.GetStringAsync("https://random.dog/woof.json").ConfigureAwait(false);
            using JsonDocument dog = JsonDocument.Parse(result);
            using var client = new WebClient();
            var uri = new Uri(dog.RootElement.GetProperty("url").GetString()!);
            string path = TempDir + Path.GetFileName(uri.LocalPath);
            await client.DownloadFileTaskAsync(uri, path).ConfigureAwait(false);
            return path;
        }

        public async Task<string> GetCatFactAsync()
        {
            using HttpClient http = _http.CreateClient();
            string result = await http.GetStringAsync("https://catfact.ninja/fact").ConfigureAwait(false);
            using JsonDocument fact = JsonDocument.Parse(result);

            return fact.RootElement.GetProperty("fact").GetString();
        }

        public async Task<List<WikiSearch>> SearchAsync(string query, int limit = 5)
        {
            using HttpClient http = _http.CreateClient();
            string result = await http.GetStringAsync($"{ApiUrl}&list=search&srsearch={query}&srlimit={limit}&format=json").ConfigureAwait(false);

            using JsonDocument json = JsonDocument.Parse(result);
            JsonElement queryElement = json.RootElement.GetProperty("query");

            JsonElement searchInfo = queryElement.GetProperty("searchinfo");
            int hits = searchInfo.GetProperty("totalhits").GetInt32();

            var results = new List<WikiSearch>();

            if (hits != 0)
            {
                JsonElement.ArrayEnumerator enumerator = queryElement.GetProperty("search").EnumerateArray();

                foreach (JsonElement page in enumerator)
                {
                    string snippet = page.GetProperty("snippet").GetString()!.Replace(@"<span class=""searchmatch"">", "**").Replace("</span>", "**");
                    results.Add(new WikiSearch
                    {
                        Title = page.GetProperty("title").GetString(),
                        Snippet = HttpUtility.HtmlDecode(snippet)
                    });
                }
            }
            else if (searchInfo.TryGetProperty("suggestion", out JsonElement suggestion))
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
            using HttpClient http = _http.CreateClient();
            string result = await http.GetStringAsync($"{ApiUrl}&list=&prop=extracts&explaintext&exsentences=3&exintro=&titles={title}&format=json").ConfigureAwait(false);

            var summary = new WikiSummary();
            using (JsonDocument json = JsonDocument.Parse(result))
            {
                JsonElement element = json.RootElement.GetProperty("query").GetProperty("pages");
                JsonProperty page = element.EnumerateObject().First();
                summary.Title = page.Value.GetProperty("title").GetString();
                summary.Extract = page.Value.GetProperty("extract").GetString();
            }

            string imageResult = await http.GetStringAsync($"{ApiUrl}&titles={title}&prop=pageimages&pithumbsize=500&format=json").ConfigureAwait(false);

            using (JsonDocument json = JsonDocument.Parse(imageResult))
            {
                JsonElement element = json.RootElement.GetProperty("query").GetProperty("pages");
                JsonProperty page = element.EnumerateObject().First();
                if (page.Value.TryGetProperty("thumbnail", out JsonElement thumbnail))
                {
                    summary.ImageUrl = thumbnail.GetProperty("source").GetString();
                }
            }

            return summary;
        }
    }
}