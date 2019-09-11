using System.Diagnostics;
using System.IO;
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
                var trim = tex.Trim();

                var proc = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "tex2pic",
                        Arguments = $"-r 150 -o ./temp/tex.png \"{trim}\"",
                        RedirectStandardOutput = false,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                proc.Start();
                proc.WaitForExit();

                await ctx.Channel.SendFileAsync("./temp/tex.png").ConfigureAwait(false);
                File.Delete("./temp/tex.png");
            }
        }
    }
}