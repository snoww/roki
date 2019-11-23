using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;

namespace Roki.Modules.Moderation
{
    public partial class Moderation : RokiTopLevelModule
    {
        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task UserDetails(IUser user)
        {
            await ctx.Channel.SendMessageAsync(
                $"UserId: `{user.Id}`\nUsername: {user.Username}\nDiscriminator: `{user.Discriminator}`\nAvatarId: `{user.AvatarId}`\n").ConfigureAwait(false);
        }
    }
}