using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Moderation.Services;

namespace Roki.Modules.Games
{
    public partial class Moderation
    {
        [Group]
        public class PowerCommands : RokiSubmodule<PowersService>
        {
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(0)]
            public async Task Mute(IUser user)
            {
                var hasPower = await _service.AvailablePower(ctx.User.Id, "Mute");
                if (!hasPower)
                {
                    await ctx.Channel.SendErrorAsync($"{ctx.User.Mention} do not have any mute powers available.").ConfigureAwait(false);
                    return;
                }

                await _service.ConsumePower(ctx.User.Id, "Mute").ConfigureAwait(false);
                await _service.MuteUser(ctx, user as IGuildUser).ConfigureAwait(false);
            }
        }
    }
}