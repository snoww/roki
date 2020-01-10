using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Searches.Models;

namespace Roki.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class XkcdCommands : RokiSubmodule
        {
            private const string XkcdUrl = "https://xkcd.com";
            private readonly IHttpClientFactory _httpFactory;

            public XkcdCommands(IHttpClientFactory httpFactory)
            {
                _httpFactory = httpFactory;
            }

            [RokiCommand, Description, Usage, Aliases, Priority(0)]
            public async Task Xkcd(int num = 0)
            {
                try
                {
                    using var http = _httpFactory.CreateClient();
                    var result = num == 0
                        ? await http.GetStringAsync($"{XkcdUrl}/info.0.json").ConfigureAwait(false)
                        : await http.GetStringAsync($"{XkcdUrl}/{num}/info.0.json").ConfigureAwait(false);

                    var xkcd = result.Deserialize<XkcdModel>();
                    var embed = new EmbedBuilder().WithOkColor()
                        .WithAuthor(xkcd.Title, "https://xkcd.com/s/919f27.ico", $"{XkcdUrl}/{xkcd.Num}")
                        .WithImageUrl(xkcd.Img)
                        .AddField("Comic #", xkcd.Num, true)
                        .AddField("Date", $"{xkcd.Month}/{xkcd.Year}", true);

                    await ctx.Channel.EmbedAsync(embed);
                }
                catch (HttpRequestException)
                {
                    await ctx.Channel.SendErrorAsync("Comic not found").ConfigureAwait(false);
                }
            }
        }
    }
}