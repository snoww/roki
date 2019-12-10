using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Moderation.Services;

namespace Roki.Modules.Moderation
{
    public partial class Moderation
    {
        [Group]
        public class PowerCommands : RokiSubmodule<PowersService>
        {
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireBotPermission(ChannelPermission.ManageRoles)]
            [Priority(0)]
            public async Task Mute(IUser user)
            {
                if (!await _service.AvailablePower(ctx.User.Id, "Mute").ConfigureAwait(false))
                {
                    await ctx.Channel.SendErrorAsync($"{ctx.User.Mention} do not have any mute powers available.").ConfigureAwait(false);
                    return;
                }

                await _service.ConsumePower(ctx.User.Id, "Mute").ConfigureAwait(false);
                await _service.MuteUser(ctx, user as IGuildUser).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireBotPermission(ChannelPermission.ManageRoles)]
            [Priority(0)]
            public async Task Block(IUser user)
            {
                if (!await _service.AvailablePower(ctx.User.Id, "Block").ConfigureAwait(false))
                {
                    await ctx.Channel.SendErrorAsync($"{ctx.User.Mention} do not have any mute powers available.").ConfigureAwait(false);
                    return;
                }
                await _service.ConsumePower(ctx.User.Id, "Block").ConfigureAwait(false);
                await _service.BlockUser(ctx, user as IGuildUser).ConfigureAwait(false);
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireBotPermission(ChannelPermission.ManageRoles)]
            [Priority(0)]
            public async Task Timeout(IUser user)
            {
                if (!await _service.AvailablePower(ctx.User.Id, "Timeout").ConfigureAwait(false))
                {
                    await ctx.Channel.SendErrorAsync($"{ctx.User.Mention} do not have any mute powers available.").ConfigureAwait(false);
                    return;
                }
                await _service.ConsumePower(ctx.User.Id, "Timeout").ConfigureAwait(false);
                await _service.TimeoutUser(ctx, user as IGuildUser).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireBotPermission(GuildPermission.ManageNicknames)]
            [Priority(0)]
            public async Task Nickname(IUser user, [Leftover] string nickname = null)
            {
                if (!await _service.AvailablePower(ctx.User.Id, "Nickname").ConfigureAwait(false))
                {
                    await ctx.Channel.SendErrorAsync($"{ctx.User.Mention} do not have any nickname powers available.").ConfigureAwait(false);
                    return;
                }
                if (user == null || user.IsBot || user.Equals(ctx.User) || string.IsNullOrWhiteSpace(nickname)) 
                    return;
                await _service.ConsumePower(ctx.User.Id, "Nickname").ConfigureAwait(false);

                await ((IGuildUser) user).ModifyAsync(u => u.Nickname = nickname.TrimTo(32, true)).ConfigureAwait(false);
            }
        }
    }
}