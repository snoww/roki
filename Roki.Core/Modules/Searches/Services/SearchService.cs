using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Discord.WebSocket;
using NLog;
using Roki.Extensions;
using Roki.Modules.Searches.Models;
using Roki.Services;

namespace Roki.Modules.Searches.Services
{
    public class SearchService : IRokiService
    {
        private readonly DiscordSocketClient _client;
        private readonly IRokiConfig _config;
        private readonly IGoogleApiService _google;
        private readonly IHttpClientFactory _httpFactory;
        private readonly Logger _log;

        public SearchService(DiscordSocketClient client, IHttpClientFactory httpFactory, IGoogleApiService google, IRokiConfig config)
        {
            _client = client;
            _httpFactory = httpFactory;
            _google = google;
            _config = config;
            _log = LogManager.GetCurrentClassLogger();
        }

        public async Task<string> GetWeatherDataAsync(float lat, float lng)
        {
            try
            {
                using var http = _httpFactory.CreateClient();
                var result = await http.GetStringAsync($"https://wttr.in/{lat},{lng}?0ATQ").ConfigureAwait(false);
                return result;
            }
            catch (Exception e)
            {
                _log.Warn(e);
                return null;
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
                var obj = await GetLocationDataAsync(arg);
                var currentSeconds = DateTime.UtcNow.UnixTimestamp();
                var timeResult = await http
                    .GetStringAsync(
                        $"https://maps.googleapis.com/maps/api/timezone/json?location={obj.Results[0].Geometry.Location.Lat},{obj.Results[0].Geometry.Location.Lng}&timestamp={currentSeconds}&key={_config.GoogleApi}")
                    .ConfigureAwait(false);
                var timeObj = timeResult.Deserialize<TimeZoneResult>();
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

        public async Task<TimeZoneResult> GetLocalDateTime(float lat, float lng)
        {
            using var http = _httpFactory.CreateClient();
            var currentSeconds = DateTime.UtcNow.UnixTimestamp();
            var timeResult = await http
                .GetStringAsync(
                    $"https://maps.googleapis.com/maps/api/timezone/json?location={lat},{lng}&timestamp={currentSeconds}&key={_config.GoogleApi}")
                .ConfigureAwait(false);
            return timeResult.Deserialize<TimeZoneResult>();
//            return (TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, timeZone.TimeZoneName), timeZone.TimeZoneName);
        }

        public async Task<GeolocationResult> GetLocationDataAsync(string location)
        {
            try
            {
                using var http = _httpFactory.CreateClient();
                var result = await http.GetStringAsync($"https://maps.googleapis.com/maps/api/geocode/json?address={location}&key={_config.GoogleApi}")
                    .ConfigureAwait(false);
                var obj = result.Deserialize<GeolocationResult>();
                if (obj?.Results == null || obj.Results.Length == 0)
                {
                    _log.Warn("Geocode lookup failed for {0}", location);
                    return null;
                }

                return obj;
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
            var movie = result.Deserialize<OmdbMovie>();
            return movie?.Title == null ? null : movie;
        }

        public async Task<string> GetRandomCatAsync()
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync("https://aws.random.cat/meow").ConfigureAwait(false);
            using var cat = JsonDocument.Parse(result);
            using var client = new WebClient();
            var uri = new Uri(cat.RootElement.GetProperty("file").GetString());
            var fileName = "./temp/" + Path.GetFileName(uri.LocalPath);
            await client.DownloadFileTaskAsync(uri, fileName).ConfigureAwait(false);
            return fileName;
        }
        
        public async Task<string> GetRandomDogAsync()
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync("https://random.dog/woof.json").ConfigureAwait(false);
            using var dog = JsonDocument.Parse(result);
            using var client = new WebClient();
            var uri = new Uri(dog.RootElement.GetProperty("url").GetString());
            var fileName = "./temp/" + Path.GetFileName(uri.LocalPath);
            await client.DownloadFileTaskAsync(uri, fileName).ConfigureAwait(false);
            return fileName;
        }

        public async Task<string> GetCatFactAsync()
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync("https://catfact.ninja/fact").ConfigureAwait(false);
            using var fact = JsonDocument.Parse(result);

            return fact.RootElement.GetProperty("fact").GetString();
        }
    }
}