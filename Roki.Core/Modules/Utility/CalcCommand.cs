using System.Net;
using System.Threading.Tasks;
using Discord.Commands;
using Roki.Common.Attributes;

namespace Roki.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class CalcCommands : RokiSubmodule
        {
            [RokiCommand, Description, Usage, Aliases]
            public async Task TexToImage([Leftover] string tex)
            {
                if (string.IsNullOrWhiteSpace(tex))
                    return;
                var encode = tex.Trim();
                tex = WebUtility.UrlEncode(encode);

                var result = $"https://chart.googleapis.com/chart?cht=tx&chl={tex}";

                await ctx.Channel.SendMessageAsync(result).ConfigureAwait(false);
            }
        }
    }
}