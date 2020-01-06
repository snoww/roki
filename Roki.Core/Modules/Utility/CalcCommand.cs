using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web;
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

                var encoded = HttpUtility.UrlEncode(tex.Trim());
                var fileName = $"{ctx.User.Username}-{Guid.NewGuid().ToString().Substring(0, 7)}";

                using (var client = new WebClient())
                {
                    client.DownloadFile($"https://math.now.sh?from={encoded}.png", $"./temp/{fileName}.png");
                }

                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "convert",
                        Arguments = $"-flatten ./temp/{fileName}.png ./temp/{fileName}.png",
                        RedirectStandardOutput = false,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                proc.Start();
                proc.WaitForExit();

                await ctx.Channel.SendFileAsync($"./temp/{fileName}.png").ConfigureAwait(false);
                File.Delete($"./temp/{fileName}.png");
            }
        }
    }
}