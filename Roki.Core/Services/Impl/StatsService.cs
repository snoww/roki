using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NLog;
using Roki.Core.Extentions;

namespace Roki.Core.Services.Impl
{
    public class StatsService : IStatsService
    {
        private readonly Logger _log;
        private readonly DiscordSocketClient _client;
        private readonly IConfiguration _config;
        private readonly DateTime _started;

        public const string BotVersion = "1.0.0";
        public string Author => "Snow#7777";
        public string Library => "Discord.Net";

        public string Heap => Math.Round((double) GC.GetTotalMemory(false) / 1.MiB(), 2)
            .ToString(CultureInfo.InvariantCulture);
        public double MessagesPerSecond => MessageCounter / GetUptime().TotalSeconds;

        private long _textChannels;
        public long TextChannels => Interlocked.Read(ref _textChannels);
        private long _voiceChannels;
        public long VoiceChannels => Interlocked.Read(ref _voiceChannels);
        private long _messageCounter;
        public long MessageCounter => Interlocked.Read(ref _messageCounter);
        private long _commandsRan;
        public long CommandsRan => Interlocked.Read(ref _commandsRan);

        private readonly Timer _carbonitexTimer;
        private readonly Timer _botlistTimer;
        private readonly Timer _dataTimer;
//        private readonly ConnectionMultiplexer _redis;
//        private readonly IHttpClientFactory _httpFactory;

        public StatsService(DiscordSocketClient client, CommandHandler cmdHandler,
            IConfiguration config, Roki roki)
        {
            _log = LogManager.GetCurrentClassLogger();
            _client = client;
            _config = config;

            _started = DateTime.UtcNow;
            _client.MessageReceived += _ => Task.FromResult(Interlocked.Increment(ref _messageCounter));
            cmdHandler.CommandExecuted += (_, e) => Task.FromResult(Interlocked.Increment(ref _commandsRan));

            _client.ChannelCreated += (c) =>
            {
                var _ = Task.Run(() =>
                {
                    if (c is ITextChannel)
                        Interlocked.Increment(ref _textChannels);
                    else if (c is IVoiceChannel)
                        Interlocked.Increment(ref _voiceChannels);
                });

                return Task.CompletedTask;
            };

            _client.ChannelDestroyed += (c) =>
            {
                var _ = Task.Run(() =>
                {
                    if (c is ITextChannel)
                        Interlocked.Decrement(ref _textChannels);
                    else if (c is IVoiceChannel)
                        Interlocked.Decrement(ref _voiceChannels);
                });

                return Task.CompletedTask;
            };

            _client.GuildAvailable += (g) =>
            {
                var _ = Task.Run(() =>
                {
                    var tc = g.Channels.Count(cx => cx is ITextChannel);
                    var vc = g.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, tc);
                    Interlocked.Add(ref _voiceChannels, vc);
                });
                return Task.CompletedTask;
            };

            _client.JoinedGuild += (g) =>
            {
                var _ = Task.Run(() =>
                {
                    var tc = g.Channels.Count(cx => cx is ITextChannel);
                    var vc = g.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, tc);
                    Interlocked.Add(ref _voiceChannels, vc);
                });
                return Task.CompletedTask;
            };

            _client.GuildUnavailable += (g) =>
            {
                var _ = Task.Run(() =>
                {
                    var tc = g.Channels.Count(cx => cx is ITextChannel);
                    var vc = g.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, -tc);
                    Interlocked.Add(ref _voiceChannels, -vc);
                });

                return Task.CompletedTask;
            };

            _client.LeftGuild += (g) =>
            {
                var _ = Task.Run(() =>
                {
                    var tc = g.Channels.Count(cx => cx is ITextChannel);
                    var vc = g.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, -tc);
                    Interlocked.Add(ref _voiceChannels, -vc);
                });

                return Task.CompletedTask;
            };
            
            var platform = "other";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                platform = "linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                platform = "osx";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                platform = "windows";
            
        }

        public void Initialize()
        {
            var guilds = _client.Guilds.ToArray();
            _textChannels = guilds.Sum(g => g.Channels.Count(cx => cx is ITextChannel));
            _voiceChannels = guilds.Sum(g => g.Channels.Count(cx => cx is IVoiceChannel));
        }

        public TimeSpan GetUptime() =>
            DateTime.UtcNow - _started;

        public string GetUptimeString(string separator = ", ")
        {
            var time = GetUptime();
            return $"{time.Days} days{separator}{time.Hours} hours{separator}{time.Minutes} minutes";
        }

    }
}