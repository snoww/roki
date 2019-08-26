using System.Diagnostics;
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
                        FileName = "./Resources/tex2pic",
                        Arguments = $"-r 150 -o tex.png \"{trim}\"",
                        RedirectStandardOutput = false,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                proc.Start();
                proc.WaitForExit();

                await ctx.Channel.SendFileAsync("tex.png").ConfigureAwait(false);
            }
        }
    }
}