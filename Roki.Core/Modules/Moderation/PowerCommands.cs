using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MongoDB.Bson;
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
            // temp solution only applies to 1 guild
            private static readonly ObjectId MuteId = ObjectId.Parse("5db876eb03eb7230a1b5bba2");
            private static readonly ObjectId BlockId = ObjectId.Parse("5dbaefbb03eb7230a1b5bba3");
            private static readonly ObjectId TimeoutId = ObjectId.Parse("5db84cbb03eb7230a1b5bba4");
            private static readonly ObjectId NickId = ObjectId.Parse("5def9e9f03eb7230a1b5bba6");
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireBotPermission(ChannelPermission.ManageRoles)]
            [Priority(0)]
            public async Task Mute(IUser user)
            {
                if (!await Service.ConsumePower(Context.User, Context.Guild.Id, MuteId).ConfigureAwait(false))
                {
                    await Context.Channel.SendErrorAsync($"{Context.User.Mention} do not have any mute powers available.").ConfigureAwait(false);
                    return;
                }

                await Service.MuteUser(Context, user as IGuildUser).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireBotPermission(ChannelPermission.ManageRoles)]
            [Priority(0)]
            public async Task Block(IUser user)
            {
                if (!await Service.ConsumePower(Context.User, Context.Guild.Id, BlockId).ConfigureAwait(false))
                {
                    await Context.Channel.SendErrorAsync($"{Context.User.Mention} do not have any mute powers available.").ConfigureAwait(false);
                    return;
                }
                await Service.BlockUser(Context, user as IGuildUser).ConfigureAwait(false);
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireBotPermission(ChannelPermission.ManageRoles)]
            [Priority(0)]
            public async Task Timeout(IUser user)
            {
                if (!await Service.ConsumePower(Context.User, Context.Guild.Id, TimeoutId).ConfigureAwait(false))
                {
                    await Context.Channel.SendErrorAsync($"{Context.User.Mention} do not have any mute powers available.").ConfigureAwait(false);
                    return;
                }
                
                await Service.TimeoutUser(Context, user as IGuildUser).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireBotPermission(GuildPermission.ManageNicknames)]
            [Priority(0)]
            public async Task Nickname(IUser user, [Leftover] string nickname = null)
            {
                if (!await Service.ConsumePower(Context.User, Context.Guild.Id, NickId).ConfigureAwait(false))
                {
                    await Context.Channel.SendErrorAsync($"{Context.User.Mention} do not have any nickname powers available.").ConfigureAwait(false);
                    return;
                }
                if (user == null || user.IsBot || user.Equals(Context.User) || string.IsNullOrWhiteSpace(nickname)) 
                    return;

                try
                {
                    await ((IGuildUser) user).ModifyAsync(u => u.Nickname = nickname.TrimTo(32, true)).ConfigureAwait(false);
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