using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Searches.Models;
using Roki.Modules.Searches.Services;
using Roki.Services;

namespace Roki.Modules.Searches
{
    public partial class Searches : RokiTopLevelModule<SearchService>
    {
        private readonly IRokiConfig _config;
        private readonly IGoogleApiService _google;
        private readonly IHttpClientFactory _httpFactory;

        public Searches(IRokiConfig config, IGoogleApiService google, IHttpClientFactory httpFactory)
        {
            _config = config;
            _google = google;
            _httpFactory = httpFactory;
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Weather([Leftover] string query = "toronto")
        {
            try
            {
                using var _ = Context.Channel.EnterTypingState();
                var location = await Service.GetLocationDataAsync(query).ConfigureAwait(false);
                if (location == null)
                {
                    await Context.Channel.SendErrorAsync("Cannot find specified location. Please try again.");
                    return;
                }

                var addr = location.Results[0];
                var result = await Service.GetWeatherDataAsync(addr.Geometry.Location.Lat, addr.Geometry.Location.Lng).ConfigureAwait(false);
                var tz = await Service.GetLocalDateTime(addr.Geometry.Location.Lat, addr.Geometry.Location.Lng).ConfigureAwait(false);
                var localDt = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, tz.TimeZoneId);
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithAuthor("Weather Report")
                        .WithDescription(addr.FormattedAddress + "\n" + Format.Code(result))
                        .WithFooter($"{localDt:HH:mm, MMM dd, yyyy}, {tz.TimeZoneName}, UTC{localDt:zz}"))
                    .ConfigureAwait(false);
            }
            catch
            {
                await Context.Channel.SendErrorAsync("Failed to get weather.");
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Time([Leftover] string query = "toronto")
        {
            if (string.IsNullOrWhiteSpace(_config.GoogleApi))
            {
                await Context.Channel.SendErrorAsync("No Google Api key provided.").ConfigureAwait(false);
                return;
            }
            
            var data = await Service.GetTimeDataAsync(query).ConfigureAwait(false);

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(data.Address)
                .WithDescription($"`{data.Time:f}, {data.TimeZoneName}, UTC{data.Time:zz}`");

            if (data.Culture != null && data.Culture.EnglishName != "English")
            {
                embed.WithDescription($"`{data.Time:f}, {data.TimeZoneName}, UTC{data.Time:zz}`\n" +
                                      $"Local: `{data.Time.ToString("f", data.Culture)}`");
            }

            await Context.Channel.EmbedAsync(embed);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Youtube([Leftover] string query = null)
        {
            if (!await ValidateQuery(query).ConfigureAwait(false))
                return;

            var result = (await _google.GetVideoLinksByKeywordAsync(query).ConfigureAwait(false)).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(result))
            {
                await Context.Channel.SendErrorAsync("No results.").ConfigureAwait(false);
                return;
            }

            await Context.Channel.SendMessageAsync(result).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Image([Leftover] string query = null)
        {
            if (!await ValidateQuery(query).ConfigureAwait(false))
                return;
            var result = await _google.GetImagesAsync(query).ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithAuthor("Image search for: " + query.TrimTo(50), "https://i.imgur.com/u1WtML5.png",
                    $"https://www.google.com/search?q={HttpUtility.UrlEncode(query)}&source=lnms&tbm=isch")
                .WithDescription(result.Link)
                .WithImageUrl(result.Link)
                .WithTitle(Context.User.ToString());
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task RandomImage([Leftover] string query = null)
        {
            if (!await ValidateQuery(query).ConfigureAwait(false))
                return;
            var result = await _google.GetImagesAsync(query, true).ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithAuthor("Image search for: " + query.TrimTo(50), "https://i.imgur.com/u1WtML5.png",
                    $"https://www.google.com/search?q={HttpUtility.UrlEncode(query)}&source=lnms&tbm=isch")
                .WithDescription(result.Link)
                .WithImageUrl(result.Link)
                .WithTitle(Context.User.ToString());
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Movie([Leftover] string query = null)
        {
            if (!await ValidateQuery(query).ConfigureAwait(false))
                return;

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            var movie = await Service.GetMovieDataAsync(query).ConfigureAwait(false);
            if (movie == null)
            {
                await Context.Channel.SendErrorAsync("No results found.").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(movie.Title)
                .WithUrl($"http://www.imdb.com/title/{movie.ImdbId}/")
                .WithDescription(movie.Plot.TrimTo(1000))
                .AddField("Rating", movie.ImdbRating, true)
                .AddField("Genre", movie.Genre, true)
                .AddField("Year", movie.Year, true)
                .WithImageUrl(movie.Poster);

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task UrbanDict([Leftover] string query = null)
        {
            if (!await ValidateQuery(query).ConfigureAwait(false))
                return;

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            query = HttpUtility.UrlEncode(query);
            using var http = _httpFactory.CreateClient();
            var response = await http.GetStringAsync($"http://api.urbandictionary.com/v0/define?term={query}")
                .ConfigureAwait(false);
            try
            {
                var items = response.Deserialize<UrbanResponse>().List;
                if (items.Any())
                    await Context.SendPaginatedMessageAsync(0, p =>
                    {
                        var item = items[p];
                        return new EmbedBuilder().WithOkColor()
                            .WithUrl(item.Permalink)
                            .WithAuthor(item.Word, "https://i.imgur.com/p1NqHdf.jpg")
                            .WithDescription(item.Definition);
                    }, items.Length, 1).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Warn(e.Message);
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task RandomCat()
        {
            try
            {
                var path = await Service.GetRandomCatAsync().ConfigureAwait(false);
                await Context.Channel.SendFileAsync(path).ConfigureAwait(false);
                File.Delete(path);
            }
            catch
            {
                await Context.Channel.SendErrorAsync("Something went wrong :(").ConfigureAwait(false);
            }
        }
        
        [RokiCommand, Description, Usage, Aliases]
        public async Task RandomDog()
        {
            try
            {
                var path = await Service.GetRandomDogAsync().ConfigureAwait(false);
                await Context.Channel.SendFileAsync(path).ConfigureAwait(false);
                File.Delete(path);
            }
            catch
            {
                await Context.Channel.SendErrorAsync("Something went wrong :(").ConfigureAwait(false);
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task CatFact()
        {
            try
            {
                var fact = await Service.GetCatFactAsync().ConfigureAwait(false);
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("Random Cat Fact")
                    .WithDescription(fact));
            }
            catch
            {
                await Context.Channel.SendErrorAsync("Something went wrong :(").ConfigureAwait(false);
            }
        }

        public async Task<bool> ValidateQuery(string query)
        {
            if (!string.IsNullOrWhiteSpace(query))
                return true;

            await Context.Channel.SendErrorAsync("No search query provided.").ConfigureAwait(false);
            return false;
        }
    }
}