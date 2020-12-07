using System.Threading.Tasks;
using Discord.Commands;
using Roki.Common.Attributes;
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
                var path = await Service.GetSID(Context, icao, sid);
                await Context.Channel.SendFileAsync(path);
            }
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task Star(string icao, [Leftover] string star)
            {
                var path = await Service.GetSTAR(Context, icao, star);
                await Context.Channel.SendFileAsync(path);
            }
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task Appr(string icao, [Leftover] string appr)
            {
                var path = await Service.GetAPPR(Context, icao, appr);
                await Context.Channel.SendFileAsync(path);
            }
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task Airport(string icao)
            {
                var path = await Service.GetAirport(Context, icao);
                await Context.Channel.SendFileAsync(path);
            }
        }
    }
}