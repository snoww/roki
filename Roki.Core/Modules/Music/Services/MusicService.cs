using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Music.Extensions;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace Roki.Modules.Music.Services
{
    public class MusicService : IRService
    {
        private readonly LavaNode _lavaNode;
        private readonly Logger _log;

        public MusicService(LavaNode lavaNode)
        {
            _lavaNode = lavaNode;
            _log = LogManager.GetCurrentClassLogger();
            OnReadyAsync().Wait();
            _lavaNode.OnTrackEnded += TrackFinished;
        }

        private async Task OnReadyAsync() => 
            await _lavaNode.ConnectAsync().ConfigureAwait(false);

        public async Task ConnectAsync(SocketVoiceChannel voiceChannel, ITextChannel textChannel)
        {
            if (_lavaNode.HasPlayer(textChannel.Guild))
            {
                return;
            }
            
            await _lavaNode.JoinAsync(voiceChannel, textChannel).ConfigureAwait(false);
        }
        
        public async Task LeaveAsync(SocketVoiceChannel voiceChannel)
            => await _lavaNode.LeaveAsync(voiceChannel).ConfigureAwait(false);

        public async Task QueueAsync(ICommandContext ctx, string query)
        {
            var player = _lavaNode.GetPlayer(ctx.Guild);
            var result = await _lavaNode.SearchYouTubeAsync(query).ConfigureAwait(false);

            if (result.LoadStatus == LoadStatus.NoMatches || result.LoadStatus == LoadStatus.LoadFailed)
            {
                await ctx.Channel.SendErrorAsync("No matches found.").ConfigureAwait(false);
                return;
            }

            var track = result.Tracks.First();
            track.Queued = ctx.User as IGuildUser;

            var embed = new EmbedBuilder().WithOkColor();
            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                player.Queue.Enqueue(track);
                embed.WithAuthor($"Queued: #{player.Queue.Count}", "http://i.imgur.com/nhKS3PT.png")
                    .WithDescription($"{track.PrettyTrack()}")
                    .WithFooter(track.PrettyFooter(player.Volume));
                var msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                msg.DeleteAfter(10);
            }
            else
            {
                await player.PlayAsync(track).ConfigureAwait(false);
                embed.WithAuthor($"Playing song", "http://i.imgur.com/nhKS3PT.png")
                    .WithDescription($"{track.PrettyTrack()}")
                    .WithFooter(track.PrettyFooter(player.Volume));
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            
            await ctx.Message.DeleteAsync().ConfigureAwait(false);
        }
        
        public async Task PauseAsync(ICommandContext ctx)
        {
            if (!await IsPlayerActive(ctx))
                return;
            var player = _lavaNode.GetPlayer(ctx.Guild);
            
            if (player.PlayerState != PlayerState.Playing)
                return;

            await player.PauseAsync().ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithDescription("Playback paused.");
            
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        public async Task ResumeAsync(ICommandContext ctx)
        {
            if (!await IsPlayerActive(ctx))
                return;
            var player = _lavaNode.GetPlayer(ctx.Guild);
            
            if (player.PlayerState != PlayerState.Paused)
                return;

            await player.ResumeAsync().ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithDescription("Playback resumed.");
            
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        public async Task SkipAsync(ICommandContext ctx)
        {
            if (!await IsPlayerActive(ctx))
                return;
            var player = _lavaNode.GetPlayer(ctx.Guild);
            
            try
            {
                await player.SkipAsync().ConfigureAwait(false);
                var embed = new EmbedBuilder().WithOkColor()
                    .WithAuthor("Skipped song")
                    .WithDescription(player.Track.PrettyFullTrack());

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            catch
            {
                await ctx.Channel.SendErrorAsync("There are no more tracks in the queue.");
            }
        }

        public async Task ListQueueAsync(ICommandContext ctx, int page = 0)
        {
            if (!await IsPlayerActive(ctx))
                return;
            var player = _lavaNode.GetPlayer(ctx.Guild);

            var queue = player.Queue.Items.Cast<LavaTrack>().ToArray();
            if (--page < -1)
                return;
            const int itemsPerPage = 10;

            if (page == -1)
                page = 0;

            var total = queue.TotalPlaytime();
            var totalStr = total.ToString(@"hh\:mm\:ss");

            EmbedBuilder QueueEmbed(int curPage)
            {
                var startAt = itemsPerPage * curPage;
                var number = 1 + startAt;
                var desc = string.Join("\n", queue
                    .Skip(startAt)
                    .Take(itemsPerPage)
                    .Select(t => $"`{number++}.` {t.PrettyFullTrack()}"));

                desc = $"`🔊` {player.Track.PrettyFullTrack()}\n\n" + desc;

                var pStatus = "";
                if (player.PlayerState == PlayerState.Paused)
                    pStatus += Format.Bold($"Player is paused. Use {Format.Code(".play")}` command to start playing.");

                if (!string.IsNullOrWhiteSpace(pStatus))
                    desc = pStatus + "\n" + desc;
                
                var embed = new EmbedBuilder().WithOkColor()
                    .WithAuthor($"Player queue - Page {curPage + 1}/{Math.Ceiling((double) queue.Length / itemsPerPage)}", "http://i.imgur.com/nhKS3PT.png")
                    .WithDescription(desc)
                    .WithFooter($"🔉 {player.Volume}% | {queue.Length} tracks | {totalStr}");
                return embed;
            }

            await ctx.SendPaginatedConfirmAsync(page, QueueEmbed, queue.Length, itemsPerPage, false).ConfigureAwait(false);
        }

        public async Task RemoveSongAsync(ICommandContext ctx, int index)
        {
            if (!await IsPlayerActive(ctx))
                return;
            var player = _lavaNode.GetPlayer(ctx.Guild);
            index -= 1;
            if (index < 0 || index > player.Queue.Count)
            {
                await ctx.Channel.SendErrorAsync("Song not in range.");
                return;
            }

            var removedSong = player.Queue.Items.ElementAt(index) as LavaTrack;

            player.Queue.RemoveAt(index);
            var embed = new EmbedBuilder().WithOkColor()
                .WithAuthor("Removed song from queue")
                .WithDescription(removedSong.PrettyFullTrack());

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        public async Task SetVolumeAsync(ICommandContext ctx, ushort volume)
        {
            if (!await IsPlayerActive(ctx))
                return;
            var player = _lavaNode.GetPlayer(ctx.Guild);

            await player.UpdateVolumeAsync(volume).ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithDescription($"Volume set to {player.Volume}.");
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        public async Task SeekAsync(ICommandContext ctx, int seconds)
        {
            if (!await IsPlayerActive(ctx))
                return;
            var player = _lavaNode.GetPlayer(ctx.Guild);

            var currTime = player.Track.Position;
            var addedTime = currTime.Add(new TimeSpan(0, 0, seconds));

            if (addedTime > player.Track.Duration)
            {
                await ctx.Channel.SendErrorAsync("Cannot seek that far.").ConfigureAwait(false);
                return;
            }

            await player.SeekAsync(addedTime).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription($"Skipped {seconds} seconds.")).ConfigureAwait(false);
        }

        private async Task TrackFinished(TrackEndedEventArgs args)
        {
            if (!args.Reason.ShouldPlayNext())
                return;

            await args.Player.TextChannel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithAuthor("Finished song", "http://i.imgur.com/nhKS3PT.png")
                .WithDescription(args.Track.PrettyTrack())
                .WithFooter(args.Track.PrettyFooter(args.Player.Volume))).ConfigureAwait(false);

            if (!args.Player.Queue.TryDequeue(out _))
            {
                await args.Player.TextChannel.SendErrorAsync("Finished player queue").ConfigureAwait(false);
                return;
            }

            await args.Player.TextChannel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithAuthor($"Playing song", "http://i.imgur.com/nhKS3PT.png")
                .WithDescription($"{args.Track.PrettyTrack()}")
                .WithFooter(args.Track.PrettyFooter(args.Player.Volume))).ConfigureAwait(false);
            await args.Player.PlayAsync(args.Player.Track).ConfigureAwait(false);
        }

        private async Task<bool> IsPlayerActive(ICommandContext ctx)
        {
            var player = _lavaNode.GetPlayer(ctx.Guild);
            if (player != null) return true;
            await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
            return false;
        }
    }
}