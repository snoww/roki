using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Music.Services;

namespace Roki.Modules.Music
{
    public partial class Music : RokiTopLevelModule<MusicService>
    {
        [RokiCommand, Description, Usage, Aliases]
        public async Task Join()
        {
            var user = ctx.User as SocketGuildUser;
            if (user.VoiceChannel is null)
            {
                await ctx.Channel.SendErrorAsync("You need to connect to a voice channel").ConfigureAwait(false);
                return;
            }

            await _service.ConnectAsync(user.VoiceChannel, ctx.Channel as ITextChannel).ConfigureAwait(false);
            await ReplyAsync($"now connected to {user.VoiceChannel.Name}");
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Play([Leftover] string query)
            => await ReplyAsync(await _service.PlayAsync(query, ctx.Guild.Id).ConfigureAwait(false)).ConfigureAwait(false);
    }
}