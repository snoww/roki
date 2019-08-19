using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using NodaTime.Extensions;
using Roki.Common.Attributes;
using Roki.Core.Extentions;
using Roki.Core.Services;
using Roki.Extentions;
using Roki.Modules.Searches.Common;
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
        public async Task Weather([Leftover] string query = "toronto")
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;
            var (forecast, address) = await _service.GetWeatherDataAsync(query).ConfigureAwait(false);
            var data = forecast.Response.Daily.Data;
            var embed = new EmbedBuilder();
            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            
            if (!forecast.IsSuccessStatus || address == null)
            {
                embed.WithErrorColor()
                    .WithDescription("City not found.");
            }
            else
            {
                embed = new EmbedBuilder().WithOkColor()
                                .WithTitle($"Current weather for {address}")
                                .WithDescription(data.Select(d => d.Summary).First())
                                .WithThumbnailUrl(WeatherIcon.GetWeatherIconUrl(forecast.Response.Currently.Icon.ToString()))
                                .AddField("Temperature", $"{(int) forecast.Response.Currently.Temperature} °C", true)
                                .AddField("Precip %", $"{data.Select(d => d.PrecipProbability).First() * 100}%", true)
                                .AddField("Humidity", $"{data.Select(d => d.Humidity).First() * 100}%", true)
                                .AddField("Wind", $"{Math.Round((double) data.Select(d => d.WindSpeed).First())} km/h {((double) data.Select(d => d.WindBearing).First()).DegreesToCardinal()}", true)
                                .AddField("UV Index", $"{data.Select(d => d.UvIndex).First()}", true)
                                .AddField("Low / High", $"{(int) data.Select(d => d.TemperatureLow).First()} °C / {(int) data.Select(d => d.TemperatureHigh).First()} °C", true)
                                .AddField("Sunrise", $"{data.Select(d => d.SunriseDateTime).First():t}", true)
                                .AddField("Sunset", $"{data.Select(d => d.SunsetDateTime).First():t}", true)
                                .WithFooter("Powered by Dark Sky");
                if (forecast.Response.Alerts != null)
                {
                    
                    
                    embed.AddField("Active Alerts", $"**{forecast.Response.Alerts.Select(d => d.Title).First()}**")
                        .AddField("Severity", $"{forecast.Response.Alerts.Select(d => d.Severity).First().FirstLetterToUpperCase()}", true)
                        .AddField("Expires", $"{forecast.Response.Alerts.Select(d => d.ExpiresDateTime).First():t}", true)
                        .AddField("Description", $"{forecast.Response.Alerts.Select(d => d.Description).First()}")
                        .AddField("Source", $"{forecast.Response.Alerts.Select(d => d.Uri).First()}", true)
                        .WithErrorColor();
                }
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