using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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
        private const string WikipediaIconUrl = "https://i.imgur.com/UA0wMvt.png";
        private const string WikipediaUrl = "https://en.wikipedia.org/wiki";
        private const string XkcdUrl = "https://xkcd.com";
        private const string WolframUrl = "https://api.wolframalpha.com/v1/result?i=";
        private const string UrbanDictUrl = "http://api.urbandictionary.com/v0/define?term=";
        private readonly IRokiConfig _config;
        private readonly IGoogleApiService _google;
        private readonly IHttpClientFactory _http;

        public Searches(IRokiConfig config, IGoogleApiService google, IHttpClientFactory http)
        {
            _config = config;
            _google = google;
            _http = http;
        }

        [RokiCommand, Usage, Description, Aliases]
        public async Task Weather([Leftover] string query = "toronto")
        {
            try
            {
                using IDisposable _ = Context.Channel.EnterTypingState();
                GeolocationResult location = await Service.GetLocationDataAsync(query).ConfigureAwait(false);
                if (location == null)
                {
                    await Context.Channel.SendErrorAsync("Cannot find specified location. Please try again.");
                    return;
                }

                GeolocationModel addr = location.Results[0];
                string result = await Service.GetWeatherDataAsync(addr.Geometry.Location.Lat, addr.Geometry.Location.Lng).ConfigureAwait(false);
                TimeZoneResult tz = await Service.GetLocalDateTime(addr.Geometry.Location.Lat, addr.Geometry.Location.Lng).ConfigureAwait(false);
                DateTimeOffset localDt = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, tz.TimeZoneId);
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
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

            TimeData data = await Service.GetTimeDataAsync(query).ConfigureAwait(false);

            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
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
        public async Task Youtube([Leftover] string query)
        {
            string? result = (await _google.GetVideoLinksByKeywordAsync(query).ConfigureAwait(false)).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(result))
            {
                await Context.Channel.SendErrorAsync("No results.").ConfigureAwait(false);
                return;
            }

            await Context.Channel.SendMessageAsync(result).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Image([Leftover] string query)
        {
            ImageResult result = await _google.GetImagesAsync(query).ConfigureAwait(false);
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithAuthor("Image search for: " + query.TrimTo(50), "https://i.imgur.com/u1WtML5.png",
                    $"https://www.google.com/search?q={HttpUtility.UrlEncode(query)}&source=lnms&tbm=isch")
                .WithDescription(result.Link)
                .WithImageUrl(result.Link)
                .WithTitle(Context.User.ToString());
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task RandomImage([Leftover] string query)
        {
            ImageResult result = await _google.GetImagesAsync(query, true).ConfigureAwait(false);
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithAuthor("Image search for: " + query.TrimTo(50), "https://i.imgur.com/u1WtML5.png",
                    $"https://www.google.com/search?q={HttpUtility.UrlEncode(query)}&source=lnms&tbm=isch")
                .WithDescription(result.Link)
                .WithImageUrl(result.Link)
                .WithTitle(Context.User.ToString());
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Movie([Leftover] string query)
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            OmdbMovie movie = await Service.GetMovieDataAsync(query).ConfigureAwait(false);
            if (movie == null)
            {
                await Context.Channel.SendErrorAsync("No results found.").ConfigureAwait(false);
                return;
            }

            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
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
        public async Task UrbanDict([Leftover] string query)
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            query = HttpUtility.UrlEncode(query);
            using HttpClient http = _http.CreateClient();
            string response = await http.GetStringAsync(UrbanDictUrl + query)
                .ConfigureAwait(false);
            try
            {
                UrbanModel[] items = response.Deserialize<UrbanResponse>().List;
                if (items.Any())
                {
                    await Context.SendPaginatedMessageAsync(0, p =>
                    {
                        UrbanModel item = items[p];
                        return new EmbedBuilder().WithDynamicColor(Context)
                            .WithUrl(item.Permalink)
                            .WithAuthor(item.Word, "https://i.imgur.com/p1NqHdf.jpg")
                            .WithDescription(item.Definition);
                    }, items.Length, 1).ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendErrorAsync($"No results for `{query}`");
                }
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
                string path = await Service.GetRandomCatAsync().ConfigureAwait(false);
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
                string path = await Service.GetRandomDogAsync().ConfigureAwait(false);
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
                string fact = await Service.GetCatFactAsync().ConfigureAwait(false);
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle("Random Cat Fact")
                    .WithDescription(fact));
            }
            catch
            {
                await Context.Channel.SendErrorAsync("Something went wrong :(").ConfigureAwait(false);
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Wikipedia([Leftover] string args)
        {
            string[] argsSplit = args.Split(' ');
            var queryBuilder = new StringBuilder();
            var showResults = false;
            foreach (string str in argsSplit)
            {
                if (!showResults && str.Equals("-q", StringComparison.OrdinalIgnoreCase))
                {
                    showResults = true;
                    continue;
                }

                queryBuilder.Append(str + " ");
            }

            string query = queryBuilder.ToString().Trim();
            using IDisposable typing = Context.Channel.EnterTypingState();
            // maybe in future only get 1 article if -q not provided
            List<WikiSearch> results = await Service.SearchAsync(query).ConfigureAwait(false);
            if (results == null || results.Count == 0)
            {
                await Context.Channel.SendErrorAsync($"Cannot find any results for: `{query}`").ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(results[0].Snippet))
            {
                await Context.Channel.SendErrorAsync($"Cannot find any results for: `{query}`, did you mean `{results[0].Title}`?");
                return;
            }

            if (!showResults)
            {
                WikiSummary article = await Service.GetSummaryAsync(results[0].Title).ConfigureAwait(false);
                await SendArticleAsync(article).ConfigureAwait(false);
                return;
            }

            var counter = 1;
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithAuthor("Wikipedia", WikipediaIconUrl)
                .WithTitle($"Search results for: `{query}`")
                .WithDescription(string.Join("\n", results
                    .Select(a => $"{counter++}. [{a.Title}]({WikipediaUrl}/{HttpUtility.UrlPathEncode(a.Title)})\n\t{a.Snippet}")));

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            // for future allow selecting article and showing it
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Xkcd(int num = 0)
        {
            try
            {
                using HttpClient http = _http.CreateClient();
                string result = num == 0
                    ? await http.GetStringAsync($"{XkcdUrl}/info.0.json").ConfigureAwait(false)
                    : await http.GetStringAsync($"{XkcdUrl}/{num}/info.0.json").ConfigureAwait(false);

                var xkcd = result.Deserialize<XkcdModel>();
                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithAuthor(xkcd.Title, "https://xkcd.com/s/919f27.ico", $"{XkcdUrl}/{xkcd.Num}")
                    .WithImageUrl(xkcd.Img)
                    .AddField("Comic #", xkcd.Num, true)
                    .AddField("Date", $"{xkcd.Month}/{xkcd.Year}", true);

                await Context.Channel.EmbedAsync(embed);
            }
            catch (HttpRequestException)
            {
                await Context.Channel.SendErrorAsync("Comic not found").ConfigureAwait(false);
            }
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Ask([Leftover] string query = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            using HttpClient http = _http.CreateClient();
            try
            {
                await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
                string result = await http.GetStringAsync($"{WolframUrl}{query}&appid={_config.WolframAlphaApi}").ConfigureAwait(false);
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle("Ask Roki")
                        .WithDescription(result))
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                await Context.Channel.SendErrorAsync($"Sorry, I don't have an answer for that question\n`{query}`").ConfigureAwait(false);
            }
        }

        private async Task SendArticleAsync(WikiSummary article)
        {
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithAuthor("Wikipedia", WikipediaIconUrl)
                .WithTitle(article.Title)
                .WithUrl($"{WikipediaUrl}/{HttpUtility.UrlPathEncode(article.Title)}")
                .WithDescription(article.Extract);

            if (!string.IsNullOrWhiteSpace(article.ImageUrl))
            {
                embed.WithImageUrl(article.ImageUrl);
            }

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
    }
}