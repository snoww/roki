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
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task Kick(IGuildUser user, string reason = null)
        {
            if (!await ModerationService.ValidPermissions(Context, Context.User as IGuildUser, user, "kick"))
            {
                return;
            }

            await user.KickAsync(reason);
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context).WithDescription($"Kicked {user} from {Context.Guild.Name}.");
            if (!string.IsNullOrWhiteSpace(reason))
            {
                embed.AddField("Reason", reason);
            }

            await Context.Channel.EmbedAsync(embed);
        }
        
        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Ban(IGuildUser user, string reason = null)
        {
            if (!await ModerationService.ValidPermissions(Context, Context.User as IGuildUser, user, "ban"))
            {
                return;
            }

            await user.BanAsync(reason: reason);
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context).WithDescription($"Banned {user} from {Context.Guild.Name}.");
            if (!string.IsNullOrWhiteSpace(reason))
            {
                embed.AddField("Reason", reason);
            }

            await Context.Channel.EmbedAsync(embed);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task Prune(IGuildUser user, int messages = 0)
        {
            if (messages == 0)
            {
                await Context.Channel.SendErrorAsync("Please specify the number of messages to prune");
                return;
            }

            if (messages >= 100)
            {
                await Context.Channel.SendErrorAsync("You can only prune up to 100 messages at a time.");
            }
            
            if (!await ModerationService.ValidPermissions(Context, Context.User as IGuildUser, user, "prune"))
            {
                return;
            }

            int prunedCount = await ModerationService.PruneMessagesAsync(Context, user, messages);
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context).WithDescription($"Pruned {prunedCount} messages from {user} in {Context.Guild.Name}."));
        }
    }
}