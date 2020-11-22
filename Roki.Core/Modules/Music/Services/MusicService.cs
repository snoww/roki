using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using Victoria.Responses.Rest;

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
            
            // check if url is youtube
            var isYtUrl = Regex.IsMatch(query, @"^(https?\:\/\/)?((www\.)?youtube\.com|youtu\.?be)\/.+$");
            
            // matches youtube playlist
            // can use to capture playlist ID if needed
            var isYtPlaylist = Regex.IsMatch(query, @"^.*(youtu.be\/|list=)([^#\&\?]*).*");

            SearchResponse result;
            
            if (isYtUrl)
            {
                result = await _lavaNode.SearchAsync(query).ConfigureAwait(false);
            }
            else
            {
                result = await _lavaNode.SearchYouTubeAsync(query).ConfigureAwait(false);
            }
            
            if (result.LoadStatus == LoadStatus.NoMatches)
            {
                await ctx.Channel.SendErrorAsync("No matches found.").ConfigureAwait(false);
                return;
            }

            if (result.LoadStatus == LoadStatus.LoadFailed)
            {
                await ctx.Channel.SendErrorAsync("Failed to load track.").ConfigureAwait(false);
                return;
            }

            if (isYtPlaylist && result.Tracks.Count > 1)
            {
                await QueuePlaylistAsync(ctx, player, result.Tracks).ConfigureAwait(false);
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
                    .WithFooter(track.PrettyFooter(player.Volume) + FormatAutoplayAndLoop(player));
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

        private static async Task QueuePlaylistAsync(ICommandContext ctx, LavaPlayer player, IReadOnlyCollection<LavaTrack> tracks)
        {
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithDescription($"Queuing {tracks.Count} tracks...")).ConfigureAwait(false);

            foreach (var track in tracks)
            {
                track.Queued = ctx.User as IGuildUser;
                player.Queue.Enqueue(track);
            }

            var embed = new EmbedBuilder().WithDynamicColor(ctx);
            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                embed.WithAuthor($"Queued: #{player.Queue.Count}", "https://i.imgur.com/VTRacvz.png")
                    .WithDescription($"Queued {tracks.Count} tracks")
                    .WithFooter($"🔉 {player.Volume}% | {ctx.User}{FormatAutoplayAndLoop(player)}");
                var msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                msg.DeleteAfter(10);
            }
            else
            {
                var track = player.Queue.First();
                await player.PlayAsync(track).ConfigureAwait(false);
                embed.WithAuthor($"Playing song, queued {tracks.Count} tracks", "https://i.imgur.com/fGNKX6x.png")
                    .WithDescription($"{track.Value.PrettyTrack()}")
                    .WithFooter(track.Value.PrettyFooter(player.Volume));
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

        }
        
        public async Task PlayAsync(ICommandContext ctx, int trackNum)
        {
            if (!IsPlayerActive(ctx.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
                return;
            }

            if (player.Queue.Count == 0)
            {
                await ctx.Channel.SendErrorAsync($"There are no tracks in queue, use `{Roki.Properties.Prefix}q` to queue some tracks first.").ConfigureAwait(false);
                return;
            }

            if (player.Queue.Count < trackNum)
            {
                await ctx.Channel.SendErrorAsync($"There are only {player.Queue.Count} songs in the queue.").ConfigureAwait(false);
                return;
            }

            LinkedListNode<LavaTrack> track;
            // search forwards
            if (player.Queue.Count / 2 >= trackNum)
            {
                track = player.Queue.First();
                for (int i = 0; i < trackNum - 1; i++)
                {
                    track = track?.Next;
                }
            }
            // search backwards
            else
            {
                var reverseTrackNum = player.Queue.Count - trackNum;
                track = player.Queue.Last();
                for (int i = 0; i < reverseTrackNum; i++)
                {
                    track = track?.Previous;
                }
            }

            if (track == null)
            {
                await ctx.Channel.SendErrorAsync("Something went wrong when trying to play that track.").ConfigureAwait(false);
                return;
            }
            
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(player.TextChannel.GuildId)
                .WithAuthor($"Playing song #{trackNum}", "https://i.imgur.com/fGNKX6x.png")
                .WithDescription($"{track.Value.PrettyTrack()}")
                .WithFooter(track.Value.PrettyFooter(player.Volume))).ConfigureAwait(false);
            await player.PlayAsync(track).ConfigureAwait(false);
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
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithDescription("Loop disabled")).ConfigureAwait(false);
            }
            else
            {
                if (player.Autoplay)
                {
                    player.Autoplay = false;
                    player.Loop = true;
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithDescription("Autoplay and Loop cannot **both** be enabled.\nAutoplay turned disabled, Loop **enabled**.")).ConfigureAwait(false);
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

        public async Task ListQueueAsync(ICommandContext ctx)
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
                            .WithFooter(FormatAutoplayAndLoop(player, false)))
                        .ConfigureAwait(false);
                    return;
                }
                
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx)
                        .WithAuthor("Player queue", "https://i.imgur.com/9ue01Qt.png")
                        .WithDescription("`🔊` " + player.Track.PrettyFullTrackWithCurrentPos() + "\n\nNo tracks in queue.")
                        .WithFooter(FormatAutoplayAndLoop(player, false)))
                    .ConfigureAwait(false);
                return;
            }

            var currentIndex = Array.IndexOf(queue, player.Track);
            
            const int itemsPerPage = 10;

            var page = (int) Math.Floor((double) currentIndex / itemsPerPage);

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
                    pStatus = Format.Bold($"Player is paused. Use {Format.Code($"{Roki.Properties.Prefix}resume")}` command to resume playback.");

                if (!string.IsNullOrWhiteSpace(pStatus))
                    desc = pStatus + "\n" + desc;
                
                var embed = new EmbedBuilder().WithDynamicColor(ctx)
                    .WithAuthor($"Player queue - Page {curPage + 1}/{Math.Ceiling((double) queue.Length / itemsPerPage)}", "https://i.imgur.com/9ue01Qt.png")
                    .WithDescription(desc)
                    .WithFooter($"🔉 {player.Volume}% | {queue.Length} tracks | {total.PrettyLength()}{FormatAutoplayAndLoop(player)}");
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

        private static string FormatAutoplayAndLoop(LavaPlayer player, bool cont = true)
        {
            var output = string.Empty;
            if (cont)
            {
                if (player.Autoplay)
                {
                    output += " | Autoplay: ON";
                }
                else
                {
                    output += " | Autoplay: OFF";
                }

                if (player.Loop)
                {
                    output += " | Loop: ON";
                }
                else
                {
                    output += " | Loop: OFF";
                }
            }
            else
            {
                output = $"Autoplay: {(player.Autoplay ? "ON" : "OFF")} | Loop: {(player.Loop ? "ON" : "OFF")}";
            }


            return output;
        }
    }
}