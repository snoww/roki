using System;
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
                if (!await Service.AvailablePower(Context.User.Id, "Mute").ConfigureAwait(false))
                {
                    await Context.Channel.SendErrorAsync($"{Context.User.Mention} do not have any mute powers available.").ConfigureAwait(false);
                    return;
                }

                await Service.ConsumePower(Context.User.Id, "Mute").ConfigureAwait(false);
                await Service.MuteUser(Context, user as IGuildUser).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireBotPermission(ChannelPermission.ManageRoles)]
            [Priority(0)]
            public async Task Block(IUser user)
            {
                if (!await Service.AvailablePower(Context.User.Id, "Block").ConfigureAwait(false))
                {
                    await Context.Channel.SendErrorAsync($"{Context.User.Mention} do not have any mute powers available.").ConfigureAwait(false);
                    return;
                }
                await Service.ConsumePower(Context.User.Id, "Block").ConfigureAwait(false);
                await Service.BlockUser(Context, user as IGuildUser).ConfigureAwait(false);
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireBotPermission(ChannelPermission.ManageRoles)]
            [Priority(0)]
            public async Task Timeout(IUser user)
            {
                if (!await Service.AvailablePower(Context.User.Id, "Timeout").ConfigureAwait(false))
                {
                    await Context.Channel.SendErrorAsync($"{Context.User.Mention} do not have any mute powers available.").ConfigureAwait(false);
                    return;
                }
                await Service.ConsumePower(Context.User.Id, "Timeout").ConfigureAwait(false);
                await Service.TimeoutUser(Context, user as IGuildUser).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireBotPermission(GuildPermission.ManageNicknames)]
            [Priority(0)]
            public async Task Nickname(IUser user, [Leftover] string nickname = null)
            {
                if (!await Service.AvailablePower(Context.User.Id, "Nickname").ConfigureAwait(false))
                {
                    await Context.Channel.SendErrorAsync($"{Context.User.Mention} do not have any nickname powers available.").ConfigureAwait(false);
                    return;
                }
                if (user == null || user.IsBot || user.Equals(Context.User) || string.IsNullOrWhiteSpace(nickname)) 
                    return;

                try
                {
                    await ((IGuildUser) user).ModifyAsync(u => u.Nickname = nickname.TrimTo(32, true)).ConfigureAwait(false);
                    await Service.ConsumePower(Context.User.Id, "Nickname").ConfigureAwait(false);
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithTitle($"{Context.User.Username} used a Nickname Power on {user.Username}")
                            .WithDescription($"Successfully changed {user.Username}'s nickname to `{nickname.TrimTo(32, true)}`"))
                        .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    await Context.Channel.SendErrorAsync("You cannot change that person's nickname").ConfigureAwait(false);
                }
            }
        }
    }
}