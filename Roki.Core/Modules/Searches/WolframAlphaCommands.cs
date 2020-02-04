using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Searches
{
    public partial class Searches
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
                    await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
                    var result = await http.GetStringAsync($"{WolframUrl}{query}&appid={_config.WolframAlphaApi}").ConfigureAwait(false);
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
        }
    }
}