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
                .WithAuthor("Image search for: " + encode.TrimTo(50), "http://i.imgur.com/G46fm8J.png",
                    "https://www.google.com/search?q=" + query + "&source=lnms&tbm=isch")
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
                .WithAuthor("Image search for: " + encode.TrimTo(50), "http://i.imgur.com/G46fm8J.png",
                    "https://www.google.com/search?q=" + query + "&source=lnms&tbm=isch")
                .WithDescription(result.Link)
                .WithImageUrl(result.Link)
                .WithTitle(ctx.User.ToString());
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