using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Searches.Models;
using Roki.Modules.Searches.Services;

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

        // TODO sanitize inputs!!
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Weather([Leftover] string query = "Toronto")
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;
            try
            {
                using var _ = ctx.Channel.EnterTypingState();
                var location = await _service.GetLocationDataAsync(query).ConfigureAwait(false);
                if (location == null)
                {
                    await ctx.Channel.SendErrorAsync("Cannot find specified location. Please try again.");
                    return;
                }

                var addr = location.Results[0];
                var result = await _service.GetWeatherDataAsync(addr.Geometry.Location.Lat, addr.Geometry.Location.Lng).ConfigureAwait(false);
                var tz = await _service.GetLocalDateTime(addr.Geometry.Location.Lat, addr.Geometry.Location.Lng).ConfigureAwait(false);
                var localDt = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, tz.TimeZoneId);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithAuthor("Weather Report")
                        .WithDescription(addr.FormattedAddress + "\n" + Format.Code(result))
                        .WithFooter($"{localDt:HH:mm, MMM dd, yyyy}, {tz.TimeZoneName}, UTC{localDt:zz}"))
                    .ConfigureAwait(false);
            }
            catch
            {
                await ctx.Channel.SendErrorAsync("Failed to get weather.");
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Time([Leftover] string query)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;
            if (string.IsNullOrWhiteSpace(_config.GoogleApi))
            {
                await ctx.Channel.SendErrorAsync("No Google Api key provided.").ConfigureAwait(false);
                return;
            }
            
            var data = await _service.GetTimeDataAsync(query).ConfigureAwait(false);

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle($"**{data.Address}**")
                .WithDescription($"```{data.Time:HH:mm} {data.TimeZoneName}```");

            await ctx.Channel.EmbedAsync(embed);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Youtube([Leftover] string query = null)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            var result = (await _google.GetVideoLinksByKeywordAsync(query).ConfigureAwait(false)).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(result))
            {
                await ctx.Channel.SendErrorAsync("No results.").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.SendMessageAsync(result).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Image([Leftover] string query = null)
        {
            var encode = query?.Trim();
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;
            query = HttpUtility.UrlEncode(encode);
            var result = await _google.GetImagesAsync(encode).ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithAuthor("Image search for: " + encode.TrimTo(50), "https://i.imgur.com/u1WtML5.png",
                    "https://www.google.com/search?q=" + query + "&source=lnms&tbm=isch")
                .WithDescription(result.Link)
                .WithImageUrl(result.Link)
                .WithTitle(ctx.User.ToString());
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task RandomImage([Leftover] string query = null)
        {
            var encode = query?.Trim();
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;
            query = HttpUtility.UrlEncode(encode);
            var result = await _google.GetImagesAsync(encode, true).ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithAuthor("Image search for: " + encode.TrimTo(50), "https://i.imgur.com/u1WtML5.png",
                    "https://www.google.com/search?q=" + query + "&source=lnms&tbm=isch")
                .WithDescription(result.Link)
                .WithImageUrl(result.Link)
                .WithTitle(ctx.User.ToString());
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Movie([Leftover] string query = null)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

            var movie = await _service.GetMovieDataAsync(query).ConfigureAwait(false);
            if (movie == null)
            {
                await ctx.Channel.SendErrorAsync("No results found.").ConfigureAwait(false);
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

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task UrbanDict([Leftover] string query = null)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            using var http = _httpFactory.CreateClient();
            var response = await http.GetStringAsync($"http://api.urbandictionary.com/v0/define?term={Uri.EscapeUriString(query)}")
                .ConfigureAwait(false);
            try
            {
                var items = response.Deserialize<UrbanResponse>().List;
                if (items.Any())
                    await ctx.SendPaginatedConfirmAsync(0, p =>
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
                _log.Warn(e.Message);
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task RandomCat()
        {
            try
            {
                var file = await _service.GetRandomCatAsync().ConfigureAwait(false);
                await ctx.Channel.SendFileAsync(file).ConfigureAwait(false);
                File.Delete(file);
            }
            catch
            {
                await ctx.Channel.SendErrorAsync("Something went wrong :(").ConfigureAwait(false);
            }
        }
        
        [RokiCommand, Description, Usage, Aliases]
        public async Task RandomDog()
        {
            try
            {
                var file = await _service.GetRandomDogAsync().ConfigureAwait(false);
                await ctx.Channel.SendFileAsync(file).ConfigureAwait(false);
                File.Delete(file);
            }
            catch
            {
                await ctx.Channel.SendErrorAsync("Something went wrong :(").ConfigureAwait(false);
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task CatFact()
        {
            try
            {
                var fact = await _service.GetCatFactAsync().ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("Random Cat Fact")
                    .WithDescription(fact));
            }
            catch
            {
                await ctx.Channel.SendErrorAsync("Something went wrong :(").ConfigureAwait(false);
            }
        }

        public async Task<bool> ValidateQuery(IMessageChannel channel, string query)
        {
            if (!string.IsNullOrWhiteSpace(query))
                return true;

            await ctx.Channel.SendErrorAsync("No search query provided.").ConfigureAwait(false);
            return false;
        }
    }
}