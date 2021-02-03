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
        
        
    }
}