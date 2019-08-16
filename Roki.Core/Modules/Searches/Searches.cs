using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Roki.Common.Attributes;
using Roki.Core.Extentions;
using Roki.Core.Services;
using Roki.Extentions;
using Roki.Modules.Searches.Services;

namespace Roki.Modules.Searches
{
    public partial class Searches : RokiTopLevelModule<SearchService>
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IGoogleApiService _google;

        public Searches(IConfiguration config, IGoogleApiService google, IHttpClientFactory httpFactory)
        {
            _config = config;
            _google = google;
            _httpFactory = httpFactory;
        }

        [RokiCommand, Usage, Description, Aliases]
        public async Task Weather([Leftover] string query = null)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;
            var forecast = await _service.GetWeatherDataAsync(query).ConfigureAwait(false);
            var data = forecast.Response.Daily.Data;
            var embed = new EmbedBuilder();
            
            if (!forecast.IsSuccessStatus)
            {
                embed.WithErrorColor()
                    .WithDescription("City not found.");
            }
            else
            {
                embed = new EmbedBuilder().WithOkColor()
                                .WithTitle($"Current weather for {forecast.Response.TimeZone}")
                                .WithDescription(forecast.Response.Daily.Summary)
                                .AddField("Temperature", $"{forecast.Response.Currently.Temperature}", true)
                                .AddField("Precip %", $"{data.Select(d => d.PrecipProbability)}", true)
                                .AddField("Humidity", $"{data.Select(d => d.Humidity)}", true)
                                .AddField("Wind", $"{data.Select(d => d.WindSpeed)}", true)
                                .AddField("UV Index", $"{data.Select(d => d.UvIndex)}", true);
            }

            await ctx.Channel.EmbedAsync(embed);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Time([Leftover] string query)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;
            if (string.IsNullOrWhiteSpace(_config.GoogleApi))
            {
                var err = new EmbedBuilder().WithErrorColor()
                    .WithDescription("No Google Api key provided.");
                await ctx.Channel.EmbedAsync(err).ConfigureAwait(false);
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
                var err = new EmbedBuilder().WithErrorColor()
                    .WithDescription("No results.");
                await ctx.Channel.EmbedAsync(err).ConfigureAwait(false);
            }

            await ctx.Channel.SendMessageAsync(result).ConfigureAwait(false);
        }
        
        [RokiCommand, Description, Usage, Aliases]
        public async Task Image([Leftover] string query = null)
        {
            // TODO search imgur if google returns no results
            var encode = query?.Trim();
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;
            query = WebUtility.UrlEncode(encode).Replace(" ", "+");
            var result = await _google.GetImagesAsync(encode).ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithAuthor("Image search for: " + encode.TrimTo(50), "https://i.imgur.com/u1WtML5.png", "https://www.google.com/search?q=" + query + "&source=lnms&tbm=isch")
                .WithDescription(result.Link)
                .WithImageUrl(result.Link)
                .WithTitle(ctx.User.ToString());
            await ctx.Channel.EmbedAsync(embed);
        }
        
        [RokiCommand, Description, Usage, Aliases]
        public async Task RandomImage([Leftover] string query = null)
        {
            // TODO search imgur if google returns no results
            var encode = query?.Trim();
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;
            query = WebUtility.UrlEncode(encode).Replace(" ", "+");
            var result = await _google.GetImagesAsync(encode, true).ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithAuthor("Image search for: " + encode.TrimTo(50), "https://i.imgur.com/u1WtML5.png", "https://www.google.com/search?q=" + query + "&source=lnms&tbm=isch")
                .WithDescription(result.Link)
                .WithImageUrl(result.Link)
                .WithTitle(ctx.User.ToString());
            await ctx.Channel.EmbedAsync(embed);
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
                var err = new EmbedBuilder().WithErrorColor()
                    .WithDescription("No results found.");
                await ctx.Channel.EmbedAsync(err);
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

            await ctx.Channel.EmbedAsync(embed);
        }

        public async Task<bool> ValidateQuery(IMessageChannel channel, string query)
        {
            if (!string.IsNullOrWhiteSpace(query))
                return true;
            
            var embed = new EmbedBuilder().WithErrorColor()
                .WithDescription("No search query provided.");
            await ctx.Channel.EmbedAsync(embed);
            return false;
        }
    }
}