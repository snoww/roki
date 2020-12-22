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
            public async Task Sid(string icao, [Leftover] string sid = null)
            {
                if (icao.Length != 4)
                {
                    await Context.Channel.SendErrorAsync("Invalid ICAO").ConfigureAwait(false);
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
            public async Task Star(string icao, [Leftover] string star = null)
            {
                if (icao.Length != 4)
                {
                    await Context.Channel.SendErrorAsync("Invalid ICAO").ConfigureAwait(false);
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
            public async Task Appr(string icao, [Leftover] string appr = null)
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
            public async Task Taxi(string icao, [Leftover] string taxi = null)
            {
                if (icao.Length != 4)
                {
                    await Context.Channel.SendErrorAsync("Invalid ICAO").ConfigureAwait(false);
                    return;
                }

                var path = await Service.GetTAXI(Context, icao, taxi);
                if (path == null)
                {
                    return;
                }
                await Context.Channel.SendFileAsync(path);
            }
        }
    }
}