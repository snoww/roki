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
            await ctx.Channel.SendMessageAsync(
                $"UserId: `{user.Id}`\nUsername: {user.Username}\nDiscriminator: `{user.Discriminator}`\nAvatarId: `{user.AvatarId}`\n").ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task Logging(bool enable = true)
        {
            await _service.LoggingChannel(ctx.Channel.Id, enable);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithDescription("This channel's messages will now be logged.\nUse `.logging false` to disable logging"))
                .ConfigureAwait(false);
        }
    }
}