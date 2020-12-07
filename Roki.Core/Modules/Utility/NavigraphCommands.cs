using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Utility.Services;

namespace Roki.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class NavigraphCommands : RokiSubmodule<NavigraphService>
        {
            [RokiCommand, Usage, Description, Aliases]
            public async Task Sid(string icao, [Leftover] string sid)
            {
                if (icao.Length != 4)
                {
                    await Context.Channel.SendErrorAsync("Invalid ICAO").ConfigureAwait(false);
                    return;
                }

                if (sid.Split().Length < 2 || !Regex.IsMatch(sid, "\\d+"))
                {
                    await Context.Channel.SendErrorAsync("Invalid SID.").ConfigureAwait(false);
                    return;
                }
                var path = await Service.GetSID(Context, icao, sid);
                if (path == null)
                {
                    return;
                }
                await Context.Channel.SendFileAsync(path);
            }
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task Star(string icao, [Leftover] string star)
            {
                if (icao.Length != 4)
                {
                    await Context.Channel.SendErrorAsync("Invalid ICAO").ConfigureAwait(false);
                    return;
                }
                if (star.Split().Length < 2 || !Regex.IsMatch(star, "\\d+"))
                {
                    await Context.Channel.SendErrorAsync("Invalid STAR.").ConfigureAwait(false);
                    return;
                }
                var path = await Service.GetSTAR(Context, icao, star);
                if (path == null)
                {
                    return;
                }
                await Context.Channel.SendFileAsync(path);
            }
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task Appr(string icao, [Leftover] string appr)
            {
                if (icao.Length != 4)
                {
                    await Context.Channel.SendErrorAsync("Invalid ICAO").ConfigureAwait(false);
                    return;
                }

                var path = await Service.GetAPPR(Context, icao, appr);
                if (path == null)
                {
                    return;
                }
                await Context.Channel.SendFileAsync(path);
            }
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task Airport(string icao)
            {
                var path = await Service.GetAirport(Context, icao);
                if (path == null)
                {
                    return;
                }
                await Context.Channel.SendFileAsync(path);
            }
        }
    }
}