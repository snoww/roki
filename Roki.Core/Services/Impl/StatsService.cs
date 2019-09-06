using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NLog;
using Roki.Extensions;

namespace Roki.Core.Services.Impl
{
    public class StatsService : IStatsService
    {
        public const string BotVersion = "0.2.1";
        private readonly DiscordSocketClient _client;
        private readonly IConfiguration _config;
        private readonly Logger _log;
        private readonly DateTime _started;
        private long _commandsRan;
        private long _messageCounter;

        private long _textChannels;
        private long _voiceChannels;

//        private readonly Timer _carbonitexTimer;
//        private readonly Timer _botlistTimer;
//        private readonly Timer _dataTimer;
//        private readonly ConnectionMultiplexer _redis;
//        private readonly IHttpClientFactory _httpFactory;

        public StatsService(DiscordSocketClient client, CommandHandler cmdHandler, IConfiguration config, Roki roki)
        {
            _log = LogManager.GetCurrentClassLogger();
            _client = client;
            _config = config;

            _started = DateTime.UtcNow;
            _client.MessageReceived += _ => Task.FromResult(Interlocked.Increment(ref _messageCounter));
            cmdHandler.CommandExecuted += (_, e) => Task.FromResult(Interlocked.Increment(ref _commandsRan));

            _client.ChannelCreated += channel =>
            {
                var _ = Task.Run(() =>
                {
                    if (channel is ITextChannel)
                        Interlocked.Increment(ref _textChannels);
                    else if (channel is IVoiceChannel)
                        Interlocked.Increment(ref _voiceChannels);
                });

                return Task.CompletedTask;
            };

            _client.ChannelDestroyed += channel =>
            {
                var _ = Task.Run(() =>
                {
                    if (channel is ITextChannel)
                        Interlocked.Decrement(ref _textChannels);
                    else if (channel is IVoiceChannel)
                        Interlocked.Decrement(ref _voiceChannels);
                });

                return Task.CompletedTask;
            };

            _client.GuildAvailable += guild =>
            {
                var _ = Task.Run(() =>
                {
                    var tc = guild.Channels.Count(cx => cx is ITextChannel);
                    var vc = guild.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, tc);
                    Interlocked.Add(ref _voiceChannels, vc);
                });
                return Task.CompletedTask;
            };

            _client.JoinedGuild += guild =>
            {
                var _ = Task.Run(() =>
                {
                    var tc = guild.Channels.Count(cx => cx is ITextChannel);
                    var vc = guild.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, tc);
                    Interlocked.Add(ref _voiceChannels, vc);
                });
                return Task.CompletedTask;
            };

            _client.GuildUnavailable += guild =>
            {
                var _ = Task.Run(() =>
                {
                    var tc = guild.Channels.Count(cx => cx is ITextChannel);
                    var vc = guild.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, -tc);
                    Interlocked.Add(ref _voiceChannels, -vc);
                });

                return Task.CompletedTask;
            };

            _client.LeftGuild += guild =>
            {
                var _ = Task.Run(() =>
                {
                    var tc = guild.Channels.Count(cx => cx is ITextChannel);
                    var vc = guild.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, -tc);
                    Interlocked.Add(ref _voiceChannels, -vc);
                });

                return Task.CompletedTask;
            };

//            var platform = "other";
//            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
//                platform = "linux";
//            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
//                platform = "osx";
//            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
//                platform = "windows";
        }

        public string Author => "Snow#7777";
        public string Library => "Discord.Net";

        public string Heap => Math.Round((double) GC.GetTotalMemory(false) / 1.MiB(), 2).ToString(CultureInfo.InvariantCulture);
        public double MessagesPerSecond => MessageCounter / GetUptime().TotalSeconds;
        public long TextChannels => Interlocked.Read(ref _textChannels);
        public long VoiceChannels => Interlocked.Read(ref _voiceChannels);
        public long MessageCounter => Interlocked.Read(ref _messageCounter);
        public long CommandsRan => Interlocked.Read(ref _commandsRan);

        public void Initialize()
        {
            var guilds = _client.Guilds.ToArray();
            _textChannels = guilds.Sum(guild => guild.Channels.Count(cx => cx is ITextChannel));
            _voiceChannels = guilds.Sum(guild => guild.Channels.Count(cx => cx is IVoiceChannel));
        }

        public TimeSpan GetUptime()
        {
            return DateTime.UtcNow - _started;
        }

        public string GetUptimeString(string separator = ", ")
        {
            var time = GetUptime();
            return $"{time.Days} days{separator}{time.Hours} hours{separator}{time.Minutes} minutes";
        }
    }
}