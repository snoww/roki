using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Music.Services;

namespace Roki.Modules.Music
{
    [RequireContext(ContextType.Guild)]
    public partial class Music : RokiTopLevelModule<MusicService>
    {
        [RokiCommand, Description, Usage, Aliases]
        public async Task Queue([Leftover] string query)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
            {
                return;
            }

            var user = Context.User as SocketGuildUser;

            await Service.ConnectAsync(user?.VoiceChannel, Context.Channel as ITextChannel).ConfigureAwait(false);
            await Service.QueueAsync(Context, query).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Play(int trackNum)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
            {
                return;
            }

            if (trackNum < 1)
            {
                await Context.Channel.SendErrorAsync("Please specify a track number to play.").ConfigureAwait(false);
                return;
            }

            await Service.PlayAsync(Context, trackNum).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Autoplay()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
            {
                return;
            }

            await Service.AutoplayAsync(Context).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task QueueLoop()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
            {
                return;
            }

            await Service.LoopAsync(Context).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Pause()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
            {
                return;
            }

            await Service.PauseAsync(Context);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Resume()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
            {
                return;
            }

            await Service.ResumeAsync(Context).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Destroy()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
            {
                return;
            }

            var user = Context.User as SocketGuildUser;

            await Service.LeaveAsync(user!.VoiceChannel).ConfigureAwait(false);

            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithDescription($"Disconnected from {user.VoiceChannel.Name}");
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Next()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
            {
                return;
            }

            await Service.SkipAsync(Context).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task ListQueue()
        {
            await Service.ListQueueAsync(Context).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task SongRemove(int index)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
            {
                return;
            }

            if (index == 0)
            {
                await Context.Channel.SendErrorAsync("Please provide the index of the song in the queue.").ConfigureAwait(false);
                return;
            }

            await Service.RemoveSongAsync(Context, index).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Volume(int volume = int.MinValue)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
            {
                return;
            }

            if (volume == int.MinValue)
            {
                int curVol = Service.GetPlayerVolume(Context.Guild);
                if (curVol == -1)
                {
                    await Context.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                    return;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context).WithDescription($"Current volume is {curVol}%"));
                return;
            }

            if (volume <= 0 || volume > 100)
            {
                await Context.Channel.SendErrorAsync("Volume must be from 0-100.").ConfigureAwait(false);
                return;
            }

            await Service.SetVolumeAsync(Context, (ushort) volume).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Seek(int seconds)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
            {
                return;
            }

            if (seconds == 0)
            {
                return;
            }
            
            if (seconds < 0)
            {
                await Context.Channel.SendErrorAsync("Cannot skip that far.").ConfigureAwait(false);
                return;
            }

            await Service.SeekAsync(Context, seconds).ConfigureAwait(false);
        }

        private async Task<bool> IsUserInVoice()
        {
            var user = Context.User as SocketGuildUser;
            if (user?.VoiceChannel != null) return true;
            await Context.Channel.SendErrorAsync("You must be connected to a voice channel.").ConfigureAwait(false);
            return false;
        }
    }
}