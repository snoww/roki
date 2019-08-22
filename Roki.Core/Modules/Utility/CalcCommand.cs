using System.IO;
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

                var result = $"https://math.now.sh?from={tex}.png";
                using (var client = new WebClient())
                {
                    // make image bigger by converting from svg???
                    client.DownloadFile(result, "tex.png");
                    await ctx.Channel.SendFileAsync("tex.png");
                    File.Delete("tex.png");
                }
            }
        }
    }
}