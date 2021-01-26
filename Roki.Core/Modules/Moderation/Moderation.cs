using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Moderation.Services;

namespace Roki.Modules.Moderation
{
    public partial class Moderation : RokiTopLevelModule<ModerationService>
    {
        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task UserDetails(IUser user)
        {
            await Context.Channel.SendMessageAsync(
                $"UserId: `{user.Id}`\nUsername: {user.Username}\nDiscriminator: `{user.Discriminator}`\nAvatarId: `{user.AvatarId}`\n").ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task Logging(string option = null)
        {
            bool enabled = Service.IsLoggingEnabled(Context.Channel as ITextChannel);
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context);

            if (option == null)
            {
                if (enabled)
                {
                    await Context.Channel.EmbedAsync(embed.WithDescription("Logging is currently enabled in this channel.\n" +
                                                                           $"Use `{Roki.Properties.Prefix}logging disable` to disable logging"))
                        .ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.EmbedAsync(embed.WithDescription("Logging is currently disabled in this channel.\n" +
                                                                           $"Use `{Roki.Properties.Prefix}logging enable` to enable logging"))
                        .ConfigureAwait(false);
                }
            }
            else if (option.Equals("enable", StringComparison.OrdinalIgnoreCase))
            {
                if (enabled)
                {
                    await Context.Channel.EmbedAsync(embed.WithDescription("Logging is already enabled in this channel.\n" +
                                                                           $"Use `{Roki.Properties.Prefix}logging disable` to disable logging"))
                        .ConfigureAwait(false);
                }
                else
                {
                    await Service.ChangeChannelLoggingAsync(Context.Channel.Id, true).ConfigureAwait(false);
                }

            }
            else if (option.Equals("disable", StringComparison.OrdinalIgnoreCase))
            {
                if (enabled)
                {
                    await Service.ChangeChannelLoggingAsync(Context.Channel.Id, false).ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.EmbedAsync(embed.WithDescription("Logging is already disabled in this channel.\n" +
                                                                           $"Use `{Roki.Properties.Prefix}logging enable` to enable logging"))
                        .ConfigureAwait(false);
                }
            }
            else
            {
                if (enabled)
                {
                    await Context.Channel.EmbedAsync(embed.WithDescription("Logging is currently enabled in this channel.\n" +
                                                                           $"Use `{Roki.Properties.Prefix}logging disable` to disable logging"))
                        .ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.EmbedAsync(embed.WithDescription("Logging is currently disabled in this channel.\n" +
                                                                           $"Use `{Roki.Properties.Prefix}logging enable` to enable logging"))
                        .ConfigureAwait(false);
                }
            }
        }
    }
}