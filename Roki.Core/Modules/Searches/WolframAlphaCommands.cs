using System;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Searches
{
    public partial class Utility
    {
        [Group]
        public class WolframAlphaCommands : RokiSubmodule
        {
            private readonly IHttpClientFactory _http;
            private readonly IRokiConfig _config;
            private const string WolframUrl = "https://api.wolframalpha.com/v1/result?i=";

            public WolframAlphaCommands(IHttpClientFactory http, IRokiConfig config)
            {
                _http = http;
                _config = config;
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Ask([Leftover] string query = null)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return;
                }

                using var http = _http.CreateClient();
                try
                {
                    await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                    var result = await http.GetStringAsync($"{WolframUrl}{query}&appid={_config.WolframAlphaApi}").ConfigureAwait(false);
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("Ask Roki")
                            .WithDescription(result))
                        .ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    await ctx.Channel.SendErrorAsync($"Sorry, I don't have an answer for that question\n`{query}`").ConfigureAwait(false);
                }
            }
        }
    }
}