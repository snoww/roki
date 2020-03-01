using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using Roki.Extensions;
using Roki.Modules.Music.Extensions;
using Roki.Services;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace Roki.Modules.Music.Services
{
    public class MusicService : IRokiService
    {
        private readonly LavaNode _lavaNode;
        private readonly IGoogleApiService _google;

        public MusicService(LavaNode lavaNode, IGoogleApiService google)
        {
            _lavaNode = lavaNode;
            _google = google;
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
            
            var player = await _lavaNode.JoinAsync(voiceChannel, textChannel).ConfigureAwait(false);
            await player.UpdateVolumeAsync(50).ConfigureAwait(false); // since volume default value is not set, it shows 0, manually setting volume here to update the property
        }
        
        public async Task LeaveAsync(SocketVoiceChannel voiceChannel)
            => await _lavaNode.LeaveAsync(voiceChannel).ConfigureAwait(false);

        public async Task QueueAsync(ICommandContext ctx, string query)
        {
            var player = _lavaNode.GetPlayer(ctx.Guild);
            if (string.IsNullOrWhiteSpace(query))
            {
                await player.TextChannel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                    .WithAuthor("Playing song", "http://i.imgur.com/nhKS3PT.png")
                    .WithDescription($"{player.Track.PrettyFullTrackWithCurrentPos()}")
                    .WithFooter(player.Track.PrettyFooter(player.Volume))).ConfigureAwait(false);
                return;
            }
            
            var result = await _lavaNode.SearchYouTubeAsync(query).ConfigureAwait(false);

            if (result.LoadStatus == LoadStatus.NoMatches || result.LoadStatus == LoadStatus.LoadFailed)
            {
                await ctx.Channel.SendErrorAsync("No matches found.").ConfigureAwait(false);
                return;
            }

            var track = result.Tracks.First();
            track.Queued = ctx.User as IGuildUser;

            var embed = new EmbedBuilder().WithDynamicColor(ctx);
            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                player.Queue.Enqueue(track);
                embed.WithAuthor($"Queued: #{player.Queue.Count}", "http://i.imgur.com/nhKS3PT.png")
                    .WithDescription($"{track.PrettyTrack()}")
                    .WithFooter(track.PrettyFooter(player.Volume) + $" | Autoplay: {(player.Autoplay ? "ON" : "OFF")}");
                var msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                msg.DeleteAfter(10);
            }
            else
            {
                await player.PlayAsync(track).ConfigureAwait(false);
                embed.WithAuthor("Playing song", "http://i.imgur.com/nhKS3PT.png")
                    .WithDescription($"{track.PrettyTrack()}")
                    .WithFooter(track.PrettyFooter(player.Volume));
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            
            await ctx.Message.DeleteAsync().ConfigureAwait(false);
        }

        public async Task AutoplayAsync(ICommandContext ctx)
        {
            if (!IsPlayerActive(ctx.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                return;
            }
            
            if (player.Autoplay)
            {
                player.Autoplay = false;
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithDescription("Autoplay disabled")).ConfigureAwait(false);
            }
            else
            {
                player.Autoplay = true;
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithDescription("Autoplay enabled")).ConfigureAwait(false);
            }
        }
        
        public async Task PauseAsync(ICommandContext ctx)
        {
            if (!IsPlayerActive(ctx.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                return;
            }
            
            if (player.PlayerState != PlayerState.Playing)
                return;

            await player.PauseAsync().ConfigureAwait(false);
            var embed = new EmbedBuilder().WithDynamicColor(ctx)
                .WithDescription("Playback paused.");

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        public async Task ResumeAsync(ICommandContext ctx)
        {
            if (!IsPlayerActive(ctx.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                return;
            }
            
            if (player.PlayerState != PlayerState.Paused)
                return;

            await player.ResumeAsync().ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithDescription("Playback resumed.");
            
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        public async Task SkipAsync(ICommandContext ctx)
        {
            if (!IsPlayerActive(ctx.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                return;
            }
            
            try
            {
                await player.SkipAsync().ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                        .WithAuthor("Skipped song")
                        .WithDescription("`🔊` Now playing:\n" + player.Track.PrettyFullTrack()))
                    .ConfigureAwait(false);
            }
            catch
            {
                await player.StopAsync().ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                        .WithAuthor("Skipped song")
                        .WithDescription("No more songs in queue."))
                    .ConfigureAwait(false);
            }
        }

        public async Task ListQueueAsync(ICommandContext ctx, int page = 0)
        {
            if (!IsPlayerActive(ctx.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                return;
            }

            var queue = player.Queue.Items.Cast<LavaTrack>().ToArray();
            if (queue.Length == 0)
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                        .WithAuthor("Player queue", "http://i.imgur.com/nhKS3PT.png")
                        .WithDescription("`🔊` " + player.Track.PrettyFullTrackWithCurrentPos() + "\n\nNo tracks in queue.")
                        .WithFooter($"Autoplay: {(player.Autoplay ? "ON" : "OFF")} | .autoplay to toggle autoplay"))
                    .ConfigureAwait(false);
                return;
            }
            
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

                desc = $"`🔊` {player.Track.PrettyFullTrackWithCurrentPos()}\n\n" + desc;

                string pStatus = null;
                if (player.PlayerState == PlayerState.Paused)
                    pStatus = Format.Bold($"Player is paused. Use {Format.Code(".play")}` command to start playing.");

                if (!string.IsNullOrWhiteSpace(pStatus))
                    desc = pStatus + "\n" + desc;
                
                var embed = new EmbedBuilder().WithDynamicColor(ctx)
                    .WithAuthor($"Player queue - Page {curPage + 1}/{Math.Ceiling((double) queue.Length / itemsPerPage)}", "http://i.imgur.com/nhKS3PT.png")
                    .WithDescription(desc)
                    .WithFooter($"🔉 {player.Volume}% | {queue.Length} tracks | {totalStr} | Autoplay: {(player.Autoplay ? "On" : "Off")}");
                return embed;
            }

            await ctx.SendPaginatedMessageAsync(page, QueueEmbed, queue.Length, itemsPerPage, false).ConfigureAwait(false);
        }

        public async Task RemoveSongAsync(ICommandContext ctx, int index)
        {
            if (!IsPlayerActive(ctx.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                return;
            }
            
            index -= 1;
            if (index < 0 || index > player.Queue.Count)
            {
                await ctx.Channel.SendErrorAsync("Song not in range.");
                return;
            }

            var removedSong = player.Queue.Items.ElementAt(index) as LavaTrack;

            player.Queue.RemoveAt(index);
            var embed = new EmbedBuilder().WithDynamicColor(ctx)
                .WithAuthor("Removed song from queue")
                .WithDescription(removedSong.PrettyFullTrack());

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        public async Task SetVolumeAsync(ICommandContext ctx, ushort volume)
        {
            if (!IsPlayerActive(ctx.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                return;
            }

            await player.UpdateVolumeAsync(volume).ConfigureAwait(false);
            var embed = new EmbedBuilder().WithDynamicColor(ctx)
                .WithDescription($"Volume set to {player.Volume}.");
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        public int GetPlayerVolume(IGuild guild)
        {
            if (!IsPlayerActive(guild, out var player))
            {
                return -1;
            }

            return player.Volume;
        }

        public async Task SeekAsync(ICommandContext ctx, int seconds)
        {
            if (!IsPlayerActive(ctx.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                return;
            }

            var currTime = player.Track.Position;
            var addedTime = currTime.Add(new TimeSpan(0, 0, seconds));

            if (addedTime > player.Track.Duration)
            {
                await ctx.Channel.SendErrorAsync("Cannot seek that far.").ConfigureAwait(false);
                return;
            }

            await player.SeekAsync(addedTime).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                .WithDescription($"Skipped {seconds} seconds.")).ConfigureAwait(false);
        }

        private async Task TrackFinished(TrackEndedEventArgs args)
        {
            if (!args.Reason.ShouldPlayNext())
                return;
            
            await args.Player.TextChannel.EmbedAsync(new EmbedBuilder().WithDynamicColor(args.Player.TextChannel.GuildId)
                .WithAuthor("Finished song", "http://i.imgur.com/nhKS3PT.png")
                .WithDescription(args.Track.PrettyTrack())
                .WithFooter(args.Track.PrettyFooter(args.Player.Volume))).ConfigureAwait(false);

            if (!args.Player.Queue.TryDequeue(out var dequeued))
            {
                if (args.Player.Autoplay)
                {
                    var nextSong = await GetNextSong(args.Track).ConfigureAwait(false);
                    var result = await _lavaNode.SearchYouTubeAsync(nextSong).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(nextSong) || result.LoadStatus == LoadStatus.NoMatches || result.LoadStatus == LoadStatus.LoadFailed)
                    {
                        await args.Player.TextChannel.SendErrorAsync("No more songs for autoplay").ConfigureAwait(false);
                        return;
                    }

                    var track = result.Tracks.First();
                    await args.Player.TextChannel.EmbedAsync(new EmbedBuilder().WithDynamicColor(args.Player.TextChannel.GuildId)
                        .WithAuthor("Playing song", "http://i.imgur.com/nhKS3PT.png")
                        .WithDescription($"{track.PrettyTrack()}")
                        .WithFooter(track.PrettyFooter(args.Player.Volume))).ConfigureAwait(false);
                    await args.Player.PlayAsync(track).ConfigureAwait(false);
                    return;
                }

                await args.Player.TextChannel.SendErrorAsync("Finished player queue").ConfigureAwait(false);
                return;
            }

            var dq = (LavaTrack) dequeued;
            await args.Player.TextChannel.EmbedAsync(new EmbedBuilder().WithDynamicColor(args.Player.TextChannel.GuildId)
                .WithAuthor("Playing song", "http://i.imgur.com/nhKS3PT.png")
                .WithDescription($"{dq.PrettyTrack()}")
                .WithFooter(dq.PrettyFooter(args.Player.Volume))).ConfigureAwait(false);
            await args.Player.PlayAsync(dq).ConfigureAwait(false);
        }

        private bool IsPlayerActive(IGuild guild, out LavaPlayer player)
        {
            player = _lavaNode.GetPlayer(guild);
            return player != null;
        }

        private async Task<string> GetNextSong(LavaTrack track)
        {
            var uri = new Uri(track.Url);
            const string related = "https://www.youtube.com/watch?v={0}";
            
            if (uri.Host != "www.youtube.com")
            {
                var search = await _google.GetRelatedVideoByQuery(track.Title).ConfigureAwait(false);
                return search == null ? null : string.Format(related, search);
            }

            var query = HttpUtility.ParseQueryString(uri.Query);

            var videoId = string.Empty;

            if (query.AllKeys.Contains("v"))
            {
                videoId = query["v"];
            }
            else
            {
                videoId = uri.Segments.Last();
            }
            
            var relatedId = await _google.GetRelatedVideo(videoId).ConfigureAwait(false);
            return relatedId == null ? null : string.Format(related, relatedId);
        }
    }
}