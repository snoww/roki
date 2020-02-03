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
        public async Task Logging(bool enable = true)
        {
            await Service.LoggingChannel(Context.Channel.Id, enable);
            
            if (enable)
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithDescription("This channel's messages will now be logged.\nUse `.logging false` to disable logging"))
                .ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithDescription("Logging is disabled in this channel.\nUse `.logging false` to enable logging"))
                    .ConfigureAwait(false);
            }
        }
    }
}