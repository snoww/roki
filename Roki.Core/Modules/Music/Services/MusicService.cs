using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NLog;
using Roki.Core.Services;
using Roki.Extensions;
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
        }

        public Task InitializeAsync()
        {
            _lavaSocketClient.Log += LogAsync;
            _lavaSocketClient.OnTrackFinished += TrackFinished;
            return Task.CompletedTask;
        }

        public async Task ConnectAsync(SocketVoiceChannel voiceChannel, ITextChannel textChannel)
            => await _lavaSocketClient.ConnectAsync(voiceChannel, textChannel).ConfigureAwait(false);

        public async Task LeaveAsync(SocketVoiceChannel voiceChannel)
            => await _lavaSocketClient.DisconnectAsync(voiceChannel).ConfigureAwait(false);

        public async Task<string> PlayAsync(string query, ulong guildId)
        {
            var player = _lavaSocketClient.GetPlayer(guildId);
            var result = await _lavaRestClient.SearchYouTubeAsync(query).ConfigureAwait(false);
            if (result.LoadType == LoadType.NoMatches || result.LoadType == LoadType.LoadFailed)
                return "No matches found";

            var track = result.Tracks.FirstOrDefault();

            if (player.IsPlaying)
            {
                player.Queue.Enqueue(track);
                return $"{track?.Title} has been added to the queue.";
            }
            await player.PlayAsync(track).ConfigureAwait(false);
            return $"Now playing: {track?.Title}";
        }

        public async Task<string> StopAsync(ulong guildId)
        {
            var player = _lavaSocketClient.GetPlayer(guildId);
            if (player is null)
                return "Error with player.";
            await player.StopAsync().ConfigureAwait(false);
            return "Music playback stopped.";
        }
        

        private async Task ClientReadyAsync()
        {
            await _lavaSocketClient.StartAsync(_client).ConfigureAwait(false);
        }

        private async Task TrackFinished(LavaPlayer player, LavaTrack track, TrackEndReason reason)
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

        private Task LogAsync(LogMessage message)
        {
            _log.Info(message);
            return Task.CompletedTask;
        }
    }
}