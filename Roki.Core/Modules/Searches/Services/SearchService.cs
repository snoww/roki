using System;
using System.Net.Http;
using System.Threading.Tasks;
using Discord.WebSocket;
using Newtonsoft.Json;
using NLog;
using Roki.Core.Services;
using Roki.Extentions;
using Roki.Modules.Searches.Common;

namespace Roki.Modules.Searches.Services
{
    public class SearchService : INService, IUnloadableService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly DiscordSocketClient _client;
        private readonly IGoogleApiService _google;
        private readonly Logger _log;
        private readonly IConfiguration _config;

        public SearchService(DiscordSocketClient client, IHttpClientFactory httpFactory, IGoogleApiService google, IConfiguration config)
        {
            _client = client;
            _httpFactory = httpFactory;
            _google = google;
            _config = config;
            _log = LogManager.GetCurrentClassLogger();
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
                    var result = await http.GetStringAsync($"https://maps.googleapis.com/maps/api/geocode/json?address={arg}&key={_config.GoogleApi}").ConfigureAwait(false);
                    var obj = JsonConvert.DeserializeObject<GeolocationResult>(result);
                    if (obj?.Results == null || obj.Results.Length == 0)
                    {
                        _log.Warn("Geocode lookup failed for {0}", arg);
                        return null;
                    }

                    var currentSeconds = DateTime.UtcNow.UnixTimestamp();
                    var timeResult = await http.GetStringAsync($"https://maps.googleapis.com/maps/api/timezone/json?location={obj.Results[0].Geometry.Location.Lat},{obj.Results[0].Geometry.Location.Lng}&timestamp={currentSeconds}&key={_config.GoogleApi}").ConfigureAwait(false);
                    var timeObj = JsonConvert.DeserializeObject<TimeZoneResult>(timeResult);
                    var time = DateTime.UtcNow.AddSeconds(timeObj.DstOffset + timeObj.RawOffset);

                    var toReturn = new TimeData
                    {
                        Address = obj.Results[0].FormattedAddress,
                        Time = time,
                        TimeZoneName = timeObj.TimeZoneName,
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
                var result = await http.GetStringAsync(string.Format("http://www.omdbapi.com/?t={0}&apikey=f9c978ed&y=&plot=full&r=json",
                    name.Trim().Replace(' ', '+'))).ConfigureAwait(false);
                var movie = JsonConvert.DeserializeObject<OmdbMovie>(result);
                if (movie?.Title == null)
                    return null;
                return movie;
            }
        }
        
        public Task Unload()
        {
            throw new System.NotImplementedException();
        }
    }
}