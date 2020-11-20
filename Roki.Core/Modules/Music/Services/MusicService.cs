using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
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
                    .WithAuthor("Playing song", "https://i.imgur.com/fGNKX6x.png")
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

            var track = result.Tracks[0];
            // set the queued property so we know which user queued the track
            track.Queued = ctx.User as IGuildUser;
            var linkedTrack = player.Queue.Enqueue(track);

            var embed = new EmbedBuilder().WithDynamicColor(ctx);
            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                embed.WithAuthor($"Queued: #{player.Queue.Count}", "https://i.imgur.com/VTRacvz.png")
                    .WithDescription($"{track.PrettyTrack()}")
                    .WithFooter(track.PrettyFooter(player.Volume) + $" | Autoplay: {(player.Autoplay ? "ON" : "OFF")}");
                var msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                msg.DeleteAfter(10);
            }
            else
            {
                await player.PlayAsync(linkedTrack).ConfigureAwait(false);
                embed.WithAuthor("Playing song", "https://i.imgur.com/fGNKX6x.png")
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
                if (player.Loop)
                {
                    player.Loop = false;
                    player.Autoplay = true;
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithDescription("Autoplay and Loop cannot **both** be enabled.\nAutoplay **enabled**, Loop disabled.")).ConfigureAwait(false);
                }
                else
                {
                    player.Autoplay = true;
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithDescription("Autoplay enabled")).ConfigureAwait(false);
                }
            }
        }

        public async Task LoopAsync(ICommandContext ctx)
        {
            if (!IsPlayerActive(ctx.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                return;
            }
            
            if (player.Loop)
            {
                player.Loop = false;
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithDescription("Autoplay disabled")).ConfigureAwait(false);
            }
            else
            {
                if (player.Autoplay)
                {
                    player.Autoplay = false;
                    player.Loop = true;
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithDescription("Autoplay and Loop cannot both be enabled.\nAutoplay turned disabled, Loop **enabled**.")).ConfigureAwait(false);
                }
                else
                {
                    player.Loop = true;
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithDescription("Loop enabled")).ConfigureAwait(false);
                }
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
            var embed = new EmbedBuilder().WithDynamicColor(ctx)
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
                        .WithAuthor("Skipped song", "https://i.imgur.com/xSDmw1M.png")
                        .WithDescription(player.Track.PrettyFullTrack() + $"\n🔊 Now playing:\n{player.Track.PrettyFullTrack()}"))
                    .ConfigureAwait(false);
            }
            catch
            {
                await player.StopAsync().ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                        .WithAuthor("Skipped song", "https://i.imgur.com/xSDmw1M.png")
                        .WithDescription(player.Track.PrettyFullTrack()))
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

            var queue = player.Queue.ToArray();
            if (queue.Length == 0)
            {
                if (player.PlayerState == PlayerState.Stopped)
                {
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                            .WithAuthor("Player queue", "https://i.imgur.com/9ue01Qt.png")
                            .WithDescription($"Queue is empty, `{Roki.Properties.Prefix}q <query>` to search and queue a track.")
                            .WithFooter($"Autoplay: {(player.Autoplay ? "ON" : "OFF")} | {Roki.Properties.Prefix}autoplay to toggle autoplay"))
                        .ConfigureAwait(false);
                    return;
                }
                
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                        .WithAuthor("Player queue", "https://i.imgur.com/9ue01Qt.png")
                        .WithDescription("`🔊` " + player.Track.PrettyFullTrackWithCurrentPos() + "\n\nNo tracks in queue.")
                        .WithFooter($"Autoplay: {(player.Autoplay ? "ON" : "OFF")} | {Roki.Properties.Prefix}autoplay to toggle autoplay"))
                    .ConfigureAwait(false);
                return;
            }
            
            if (--page < -1)
                return;
            const int itemsPerPage = 10;

            if (page == -1)
                page = 0;

            var total = queue.TotalPlaytime();

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
                    .WithAuthor($"Player queue - Page {curPage + 1}/{Math.Ceiling((double) queue.Length / itemsPerPage)}", "https://i.imgur.com/9ue01Qt.png")
                    .WithDescription(desc)
                    .WithFooter($"🔉 {player.Volume}% | {queue.Length} tracks | {total.PrettyLength()} | Autoplay: {(player.Autoplay ? "ON" : "OFF")}");
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
            
            player.Queue.RemoveAt(index);
            var embed = new EmbedBuilder().WithDynamicColor(ctx)
                .WithAuthor("Removed song from queue")
                .WithDescription(player.Queue.ElementAt(index).PrettyFullTrack());

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

            var addedTime = player.Track.Position.Add(new TimeSpan(0, 0, seconds));

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
            if (args.Reason != TrackEndReason.Finished && args.Reason != TrackEndReason.LoadFailed && args.Reason != TrackEndReason.Stopped)
                return;

            if (args.Reason != TrackEndReason.Stopped)
                await args.Player.TextChannel.EmbedAsync(new EmbedBuilder().WithDynamicColor(args.Player.TextChannel.GuildId)
                    .WithAuthor("Finished song", "https://i.imgur.com/VTRacvz.png")
                    .WithDescription(args.Track.PrettyTrack())
                    .WithFooter($"🔉 {args.Player.Volume}% | {args.Track.Duration.PrettyLength()}"))
                    .ConfigureAwait(false);

            var nextTrack = args.Player.LinkedListTrack.Next;
            if (nextTrack == null)
            {
                // loop and autoplay are mutually exclusive
                // i.e. cannot have loop and autoplay active at the same time
                if (args.Player.Loop)
                {
                    // loop back to beginning of linked list
                    var firstTrack = args.Player.Queue.First();
                    await args.Player.TextChannel.EmbedAsync(new EmbedBuilder().WithDynamicColor(args.Player.TextChannel.GuildId)
                        .WithAuthor("Playing song", "https://i.imgur.com/fGNKX6x.png")
                        .WithDescription($"{firstTrack.Value.PrettyTrack()}")
                        .WithFooter(firstTrack.Value.PrettyFooter(args.Player.Volume))).ConfigureAwait(false);
                    await args.Player.PlayAsync(firstTrack).ConfigureAwait(false);
                    return;
                }
                if (args.Player.Autoplay)
                {
                    var nextSong = await GetNextSong(args.Track).ConfigureAwait(false);
                    var result = await _lavaNode.SearchYouTubeAsync(nextSong).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(nextSong) || result.LoadStatus == LoadStatus.NoMatches || result.LoadStatus == LoadStatus.LoadFailed)
                    {
                        await args.Player.TextChannel.SendErrorAsync("No more songs for autoplay").ConfigureAwait(false);
                        return;
                    }

                    var track = args.Player.Queue.Enqueue(result.Tracks[0]);

                    await args.Player.TextChannel.EmbedAsync(new EmbedBuilder().WithDynamicColor(args.Player.TextChannel.GuildId)
                        .WithAuthor("Playing song", "https://i.imgur.com/fGNKX6x.png")
                        .WithDescription($"{track.Value.PrettyTrack()}")
                        .WithFooter(track.Value.PrettyFooter(args.Player.Volume))).ConfigureAwait(false);
                    await args.Player.PlayAsync(track).ConfigureAwait(false);
                    return;
                }

                await args.Player.TextChannel.SendErrorAsync("Finished player queue").ConfigureAwait(false);
                return;
            }

            await args.Player.TextChannel.EmbedAsync(new EmbedBuilder().WithDynamicColor(args.Player.TextChannel.GuildId)
                .WithAuthor("Playing song", "https://i.imgur.com/fGNKX6x.png")
                .WithDescription($"{nextTrack.Value.PrettyTrack()}")
                .WithFooter(nextTrack.Value.PrettyFooter(args.Player.Volume))).ConfigureAwait(false);
            await args.Player.PlayAsync(nextTrack).ConfigureAwait(false);
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

            string videoId;

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