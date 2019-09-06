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
using Victoria.Entities;

namespace Roki.Modules.Music.Services
{
    public class MusicService : IRService
    {
        private readonly LavaRestClient _lavaRestClient;
        private readonly LavaSocketClient _lavaSocketClient;
        private readonly DiscordSocketClient _client;
        private readonly Logger _log;

        public MusicService(LavaRestClient lavaRestClient, LavaSocketClient lavaSocketClient, DiscordSocketClient client)
        {
            _client = client;
            _lavaRestClient = lavaRestClient;
            _lavaSocketClient = lavaSocketClient;
            _log = LogManager.GetCurrentClassLogger();
            OnReady();
            _lavaSocketClient.OnTrackFinished += TrackFinished;
        }

        public async Task ConnectAsync(SocketVoiceChannel voiceChannel, ITextChannel textChannel)
            => await _lavaSocketClient.ConnectAsync(voiceChannel, textChannel).ConfigureAwait(false);
        
        public async Task LeaveAsync(SocketVoiceChannel voiceChannel)
            => await _lavaSocketClient.DisconnectAsync(voiceChannel).ConfigureAwait(false);

        public async Task QueueAsync(ICommandContext ctx, string query)
        {
            var player = _lavaSocketClient.GetPlayer(ctx.Guild.Id);
            var result = await _lavaRestClient.SearchYouTubeAsync(query).ConfigureAwait(false);

            if (result.LoadType == LoadType.NoMatches || result.LoadType == LoadType.LoadFailed)
            {
                await ctx.Channel.SendErrorAsync("No matches found.").ConfigureAwait(false);
                return;
            }

            var track = result.Tracks.FirstOrDefault();

            var embed = new EmbedBuilder().WithOkColor();
            if (player.IsPlaying)
            {
                player.Queue.Enqueue(track);
                embed.WithAuthor($"Queued: #{player.Queue.Count}", "http://i.imgur.com/nhKS3PT.png")
                    .WithDescription($"{track?.Uri}");
            }
            else
            {
                await player.PlayAsync(track).ConfigureAwait(false);
                embed.WithAuthor($"Playing {track?.Title.TrimTo(65)}", "http://i.imgur.com/nhKS3PT.png")
                    .WithDescription($"{track?.Uri}");
            }
            
            await ctx.Channel.EmbedAsync(embed);
        }
        
        public async Task PauseAsync(ICommandContext ctx)
        {
            if (!await IsPlayerActive(ctx))
                return;
            var player = _lavaSocketClient.GetPlayer(ctx.Guild.Id);
            
            await player.PauseAsync().ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithDescription("Music playback paused.");
            
            await ctx.Channel.EmbedAsync(embed);
        }

        public async Task SkipAsync(ICommandContext ctx)
        {
            if (!await IsPlayerActive(ctx))
                return;
            var player = _lavaSocketClient.GetPlayer(ctx.Guild.Id);
            
            try
            {
                var currTrack = player.CurrentTrack;
                await player.SkipAsync().ConfigureAwait(false);
                var embed = new EmbedBuilder().WithOkColor()
                    .WithDescription($"Skipped song: {currTrack.Title}");

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
            var player = _lavaSocketClient.GetPlayer(ctx.Guild.Id);
            
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

                desc = $"`🔊` {player.CurrentTrack.PrettyFullTrack()}\n\n" + desc;

                var pStatus = "";
                if (player.IsPaused)
                    pStatus += Format.Bold($"Player is paused. Use {Format.Code(".play")}` command to start playing.");

                if (!string.IsNullOrWhiteSpace(pStatus))
                    desc = pStatus + "\n" + desc;
                
                var embed = new EmbedBuilder().WithOkColor()
                    .WithAuthor($"Player queue - Page {curPage + 1}/{Math.Ceiling((double) queue.Length / itemsPerPage)}", "http://i.imgur.com/nhKS3PT.png")
                    .WithDescription(desc)
                    .WithFooter($"🔉 {player.CurrentVolume}% | {queue.Length} tracks | {totalStr}");
                return embed;
            }

            await ctx.SendPaginatedConfirmAsync(page, QueueEmbed, queue.Length, itemsPerPage, false).ConfigureAwait(false);
        }

//        public async Task RemoveSongAsync(ICommandContext ctx, int index)
//        {
//            if (!await IsPlayerActive(ctx))
//                return;
//            var player = _lavaSocketClient.GetPlayer(ctx.Guild.Id);
//            
//        }

        public async Task SetVolumeAsync(ICommandContext ctx, int volume)
        {
            if (!await IsPlayerActive(ctx))
                return;
            var player = _lavaSocketClient.GetPlayer(ctx.Guild.Id);

            await player.SetVolumeAsync(volume).ConfigureAwait(false);
            var embed = new EmbedBuilder().WithOkColor()
                .WithDescription($"Volume set to {player.CurrentVolume}.");
            await ctx.Channel.EmbedAsync(embed);
        }

        private async Task OnReady()
            => await _lavaSocketClient.StartAsync(_client).ConfigureAwait(false);

        private static async Task TrackFinished(LavaPlayer player, LavaTrack track, TrackEndReason reason)
        {
            if (!reason.ShouldPlayNext())
                return;

            if (!player.Queue.TryDequeue(out var item) || !(item is LavaTrack nextTrack))
            {
                await player.TextChannel.SendErrorAsync("There are no more tracks in the queue").ConfigureAwait(false);
                return;
            }

            await player.PlayAsync(nextTrack);
        }

        private async Task<bool> IsPlayerActive(ICommandContext ctx)
        {
            var player = _lavaSocketClient.GetPlayer(ctx.Guild.Id);
            if (player != null) return true;
            await ctx.Channel.SendErrorAsync("No music player active.").ConfigureAwait(false);
            return false;
        }
    }
}