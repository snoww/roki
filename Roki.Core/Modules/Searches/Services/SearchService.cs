using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Discord.WebSocket;
using NLog;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Searches.Common;

namespace Roki.Modules.Searches.Services
{
    public class SearchService : IRService, IUnloadableService
    {
        private readonly DiscordSocketClient _client;
        private readonly IRokiConfig _config;
        private readonly IGoogleApiService _google;
        private readonly IHttpClientFactory _httpFactory;
        private readonly Logger _log;
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions{PropertyNameCaseInsensitive = true};

        public SearchService(DiscordSocketClient client, IHttpClientFactory httpFactory, IGoogleApiService google, IRokiConfig config)
        {
            _client = client;
            _httpFactory = httpFactory;
            _google = google;
            _config = config;
            _log = LogManager.GetCurrentClassLogger();
        }

        public Task Unload()
        {
            throw new NotImplementedException();
        }
        
        public async Task GetWeatherDataAsync(string query)
        {
            try
            {
                using var client = new WebClient();
                await client.DownloadFileTaskAsync(new Uri($"https://wttr.in/{query}_0Fmnpqt.png"), "./temp/weather.png");
            }
            catch (Exception e)
            {
                _log.Warn(e);
            }
        }

        public Task<TimeData> GetTimeDataAsync(string arg)
        {
            return GetTimeDataFactory(arg);
        }

        private async Task<TimeData> GetTimeDataFactory(string arg)
        {
            try
            {
                using var http = _httpFactory.CreateClient();
                var result = await http.GetStringAsync($"https://maps.googleapis.com/maps/api/geocode/json?address={arg}&key={_config.GoogleApi}")
                    .ConfigureAwait(false);
                var obj = JsonSerializer.Deserialize<GeolocationResult>(result, Options);
                if (obj?.Results == null || obj.Results.Length == 0)
                {
                    _log.Warn("Geocode lookup failed for {0}", arg);
                    return null;
                }

                var currentSeconds = DateTime.UtcNow.UnixTimestamp();
                var timeResult = await http
                    .GetStringAsync(
                        $"https://maps.googleapis.com/maps/api/timezone/json?location={obj.Results[0].Geometry.Location.Lat},{obj.Results[0].Geometry.Location.Lng}&timestamp={currentSeconds}&key={_config.GoogleApi}")
                    .ConfigureAwait(false);
                var timeObj = JsonSerializer.Deserialize<TimeZoneResult>(timeResult, Options);
                var time = DateTime.UtcNow.AddSeconds(timeObj.DstOffset + timeObj.RawOffset);

                var toReturn = new TimeData
                {
                    Address = obj.Results[0].FormattedAddress,
                    Time = time,
                    TimeZoneName = timeObj.TimeZoneName
                };
                return toReturn;
            }
            catch (Exception e)
            {
                _log.Warn(e);
                return null;
            }
        }

        public Task<OmdbMovie> GetMovieDataAsync(string name)
        {
            name = name.Trim().ToLowerInvariant();
            return GetMovieDataFactory(name);
        }

        private async Task<OmdbMovie> GetMovieDataFactory(string name)
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync($"http://www.omdbapi.com/?t={name.Trim().Replace(' ', '+')}&apikey={_config.OmdbApi}&y=&plot=full&r=json").ConfigureAwait(false);
            var movie = JsonSerializer.Deserialize<OmdbMovie>(result, Options);
            return movie?.Title == null ? null : movie;
        }

        public async Task<string> GetRandomCatAsync()
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync("https://aws.random.cat/meow").ConfigureAwait(false);
            var cat = JsonDocument.Parse(result).RootElement.GetProperty("file").GetString();

            using var client = new WebClient();
            var uri = new Uri(cat);
            var fileName = "./temp/" + Path.GetFileName(uri.LocalPath);
            await client.DownloadFileTaskAsync(uri, fileName).ConfigureAwait(false);
            return fileName;
        }
        
        public async Task<string> GetRandomDogAsync()
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync("https://random.dog/woof.json").ConfigureAwait(false);
            var dog = JsonDocument.Parse(result).RootElement.GetProperty("url").GetString();

            using var client = new WebClient();
            var uri = new Uri(dog);
            var fileName = "./temp/" + Path.GetFileName(uri.LocalPath);
            await client.DownloadFileTaskAsync(uri, fileName).ConfigureAwait(false);
            return fileName;
        }

        public async Task<string> GetCatFactAsync()
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync("https://catfact.ninja/fact").ConfigureAwait(false);
            var fact = JsonDocument.Parse(result).RootElement.GetProperty("fact").GetString();

            return fact;
        }
    }
}