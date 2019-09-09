using System;
using System.Net.Http;
using System.Threading.Tasks;
using Discord.WebSocket;
using Newtonsoft.Json;
using NLog;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Searches.Common;

namespace Roki.Modules.Searches.Services
{
    public class SearchService : IRService, IUnloadableService
    {
        private readonly DiscordSocketClient _client;
        private readonly IConfiguration _config;
        private readonly IGoogleApiService _google;
        private readonly IHttpClientFactory _httpFactory;
        private readonly Logger _log;

        public SearchService(DiscordSocketClient client, IHttpClientFactory httpFactory, IGoogleApiService google, IConfiguration config)
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
        
        public async Task<string> GetWeatherDataAsync(string query)
        {
            try
            {
                using (var http = _httpFactory.CreateClient())
                {
                    var result = await http.GetStringAsync($"http://wttr.in/{query}?A&T").ConfigureAwait(false);
                    var index = result.IndexOf('┘', result.IndexOf('┘') + 1);
                    return result.Substring(0, index + 1);
                }

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
                using (var http = _httpFactory.CreateClient())
                {
                    var result = await http.GetStringAsync($"https://maps.googleapis.com/maps/api/geocode/json?address={arg}&key={_config.GoogleApi}")
                        .ConfigureAwait(false);
                    var obj = JsonConvert.DeserializeObject<GeolocationResult>(result);
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
                    var timeObj = JsonConvert.DeserializeObject<TimeZoneResult>(timeResult);
                    var time = DateTime.UtcNow.AddSeconds(timeObj.DstOffset + timeObj.RawOffset);

                    var toReturn = new TimeData
                    {
                        Address = obj.Results[0].FormattedAddress,
                        Time = time,
                        TimeZoneName = timeObj.TimeZoneName
                    };
                    return toReturn;
                }
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
            using (var http = _httpFactory.CreateClient())
            {
                var result = await http.GetStringAsync($"http://www.omdbapi.com/?t={name.Trim().Replace(' ', '+')}&apikey={_config.OmdbApi}&y=&plot=full&r=json").ConfigureAwait(false);
                var movie = JsonConvert.DeserializeObject<OmdbMovie>(result);
                return movie?.Title == null ? null : movie;
            }
        }
    }
}