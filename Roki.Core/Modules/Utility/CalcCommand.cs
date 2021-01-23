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
            private static readonly string TempDir = Path.GetTempPath();

            [RokiCommand, Description, Usage, Aliases]
            public async Task TexToImage([Leftover] string tex)
            {
                if (string.IsNullOrWhiteSpace(tex))
                {
                    return;
                }

                string encoded = HttpUtility.UrlEncode(tex.Trim());
                var filePath = $"{TempDir}/{Context.User.Username}-{Guid.NewGuid().ToString().Substring(0, 7)}.png";

                using (var client = new WebClient())
                {
                    client.DownloadFile($"https://math.now.sh?from={encoded}.png", filePath);
                }

                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "convert",
                        Arguments = $"-flatten {filePath} {filePath}",
                        RedirectStandardOutput = false,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                await proc.WaitForExitAsync();

                await Context.Channel.SendFileAsync(filePath).ConfigureAwait(false);
                File.Delete(filePath);
            }
        }
    }
}