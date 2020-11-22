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
    public partial class Music : RokiTopLevelModule<MusicService>
    {
        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Queue([Leftover] string query = null)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            var user = Context.User as SocketGuildUser;
            
            await Service.ConnectAsync(user?.VoiceChannel, Context.Channel as ITextChannel).ConfigureAwait(false);
            await Service.QueueAsync(Context, query).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Play(int trackNum = 0)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;

            if (trackNum < 1)
            {
                await Context.Channel.SendErrorAsync("Please specify a track number to play.").ConfigureAwait(false);
                return;
            }

            await Service.PlayAsync(Context, trackNum).ConfigureAwait(false);
        }
        
        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Autoplay()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            await Service.AutoplayAsync(Context).ConfigureAwait(false);
        }
        
        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QueueLoop()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            await Service.LoopAsync(Context).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Pause()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            await Service.PauseAsync(Context);
        }
        
        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Resume()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            await Service.ResumeAsync(Context).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Destroy()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            var user = Context.User as SocketGuildUser;
            
            await Service.LeaveAsync(user.VoiceChannel).ConfigureAwait(false);
            
            var embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithDescription($"Disconnected from {user.VoiceChannel.Name}");
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Next()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            await Service.SkipAsync(Context).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ListQueue()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            
            await Service.ListQueueAsync(Context).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SongRemove(int index = 0)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;

            if (index == 0)
            {
                await Context.Channel.SendErrorAsync("Please provide the index of the song in the queue.").ConfigureAwait(false);
                return;
            }

            await Service.RemoveSongAsync(Context, index).ConfigureAwait(false);
        }
        
        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Volume(int volume = 0)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            if (volume == 0)
            {
                var curVol = Service.GetPlayerVolume(Context.Guild);
                if (curVol == -1)
                {
                    await Context.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                    return;
                }
                
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context).WithDescription($"Current volume is {curVol}%"));
                return;
            }
            
            if (volume < 0 || volume > 100)
            {
                await Context.Channel.SendErrorAsync("Volume must be from 1-100.").ConfigureAwait(false);
                return;
            }
            
            await Service.SetVolumeAsync(Context, (ushort) volume).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Seek(int seconds = 0)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            if (seconds <= 0)
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