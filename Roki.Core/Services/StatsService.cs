using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NLog;
using Roki.Extensions;

namespace Roki.Services
{
    public interface IStatsService : IRokiService
    {
        string Author { get; }
        long CommandsRan { get; }
        string Heap { get; }
        string Library { get; }
        long MessageCounter { get; }
        double MessagesPerSecond { get; }
        long TextChannels { get; }
        long VoiceChannels { get; }

        TimeSpan GetUptime();
        string GetUptimeString(string separator);
        void Initialize();
    }
    
    public class StatsService : IStatsService
    {
        private readonly DiscordSocketClient _client;
        private readonly Logger _log;
        private readonly DateTime _started;
        private long _commandsRan;
        private long _messageCounter;

        private long _textChannels;
        private long _voiceChannels;

        private const long Megabyte = 1000000;
        
        public const string BotVersion = "0.9.2";
        public string Author => "<@!125025504548880384>"; // mentions Snow#7777
        public string Library => "Discord.Net";

        public string Heap => Math.Round((double) GC.GetTotalMemory(false) / Megabyte, 2).ToString(CultureInfo.InvariantCulture);
        public double MessagesPerSecond => MessageCounter / GetUptime().TotalSeconds;
        public long TextChannels => Interlocked.Read(ref _textChannels);
        public long VoiceChannels => Interlocked.Read(ref _voiceChannels);
        public long MessageCounter => Interlocked.Read(ref _messageCounter);
        public long CommandsRan => Interlocked.Read(ref _commandsRan);


        public StatsService(DiscordSocketClient client, CommandHandler handler)
        {
            _log = LogManager.GetCurrentClassLogger();
            _client = client;

            _started = DateTime.UtcNow;
            _client.MessageReceived += _ => Task.FromResult(Interlocked.Increment(ref _messageCounter));
            handler.CommonOnSuccess += (_, e) => Task.FromResult(Interlocked.Increment(ref _commandsRan));

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
        }

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

        public string GetUptimeString(string separator)
        {
            var time = GetUptime();
            return time.ToReadableString(separator);
        }
    }
}