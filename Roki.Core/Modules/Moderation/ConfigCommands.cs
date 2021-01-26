using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;

namespace Roki.Modules.Moderation
{
    public partial class Moderation
    {
        public class ConfigCommands : RokiSubmodule
        {
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task GuildConfig([Leftover] string config = null)
            {
            
            }
        
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task ChannelConfig([Leftover] string config = null)
            {
            
            }
        }
    }
}