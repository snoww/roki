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
        public async Task Queue([Leftover] string query)
        {
            var user = ctx.User as SocketGuildUser;
            if (user?.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync("You need to connect to a voice channel").ConfigureAwait(false);
                return;
            }
            
            await _service.ConnectAsync(user.VoiceChannel, ctx.Channel as ITextChannel).ConfigureAwait(false);
            await _service.QueueAsync(ctx, query).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Pause()
        {
            var user = ctx.User as SocketGuildUser;
            if (user?.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync("You need to connect to a voice channel").ConfigureAwait(false);
                return;
            }

            await _service.PauseAsync(ctx, ctx.Guild.Id);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Destroy()
        {
            var user = ctx.User as SocketGuildUser;
            if (user?.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync("You need to connect to a voice channel").ConfigureAwait(false);
                return;
            }
            await _service.LeaveAsync(user.VoiceChannel).ConfigureAwait(false);
            
            var embed = new EmbedBuilder().WithOkColor()
                .WithDescription($"Disconnected from {user.VoiceChannel.Name}");
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

//        [RokiCommand, Description, Usage, Aliases]
//        public async Task ListQueue([Leftover] string query)
//        {
//            
//        }
    }
}