using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Extensions;

namespace Roki.Services
{
    public interface IStatsService : IRokiService
    {
        string Author { get; }
        string AuthorUsername { get; }
        long CommandsRan { get; }
        string Heap { get; }
        string Library { get; }
        long MessageCounter { get; }
        double MessagesPerSecond { get; }
        long TextChannels { get; }
        long VoiceChannels { get; }
        long Guilds { get; }

        TimeSpan GetUptime();
        string GetUptimeString(string separator);
        void Initialize();
    }

    public class StatsService : IStatsService
    {
        private const long Megabyte = 1000000;

        public const string BotVersion = "1.0.0-beta.3";
        private readonly DiscordSocketClient _client;
        private readonly DateTime _started;
        private long _commandsRan;
        private long _messageCounter;

        private long _textChannels;
        private long _voiceChannels;
        private long _guilds;


        public StatsService(DiscordSocketClient client, CommandHandler handler)
        {
            _client = client;

            _started = DateTime.UtcNow;
            _client.MessageReceived += _ => Task.FromResult(Interlocked.Increment(ref _messageCounter));
            handler.CommandOnSuccess += (_, _) => Task.FromResult(Interlocked.Increment(ref _commandsRan));

            _client.ChannelCreated += channel =>
            {
                Task _ = Task.Run(() =>
                {
                    if (channel is ITextChannel)
                    {
                        Interlocked.Increment(ref _textChannels);
                    }
                    else if (channel is IVoiceChannel)
                    {
                        Interlocked.Increment(ref _voiceChannels);
                    }
                });

                return Task.CompletedTask;
            };

            _client.ChannelDestroyed += channel =>
            {
                Task _ = Task.Run(() =>
                {
                    if (channel is ITextChannel)
                    {
                        Interlocked.Decrement(ref _textChannels);
                    }
                    else if (channel is IVoiceChannel)
                    {
                        Interlocked.Decrement(ref _voiceChannels);
                    }
                });

                return Task.CompletedTask;
            };

            _client.GuildAvailable += guild =>
            {
                Task _ = Task.Run(() =>
                {
                    int tc = guild.Channels.Count(x => x is ITextChannel);
                    int vc = guild.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, tc);
                    Interlocked.Add(ref _voiceChannels, vc);
                    Interlocked.Add(ref _guilds, 1);
                });
                return Task.CompletedTask;
            };

            _client.JoinedGuild += guild =>
            {
                Task _ = Task.Run(() =>
                {
                    int tc = guild.Channels.Count(x => x is ITextChannel);
                    int vc = guild.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, tc);
                    Interlocked.Add(ref _voiceChannels, vc);
                    Interlocked.Add(ref _guilds, 1);
                });
                return Task.CompletedTask;
            };

            _client.GuildUnavailable += guild =>
            {
                Task _ = Task.Run(() =>
                {
                    int tc = guild.Channels.Count(x => x is ITextChannel);
                    int vc = guild.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, -tc);
                    Interlocked.Add(ref _voiceChannels, -vc);
                    Interlocked.Decrement(ref _guilds);
                });

                return Task.CompletedTask;
            };

            _client.LeftGuild += guild =>
            {
                Task _ = Task.Run(() =>
                {
                    int tc = guild.Channels.Count(x => x is ITextChannel);
                    int vc = guild.Channels.Count - tc;
                    Interlocked.Add(ref _textChannels, -tc);
                    Interlocked.Add(ref _voiceChannels, -vc);
                    Interlocked.Decrement(ref _guilds);
                });

                return Task.CompletedTask;
            };
        }

        public string Author => "<@!125025504548880384>"; // mentions Snow#7777
        public string AuthorUsername => "Snow#7777";
        public string Library => "Discord.Net";

        public string Heap => Math.Round((double) GC.GetTotalMemory(false) / Megabyte, 2).ToString(CultureInfo.InvariantCulture);
        public double MessagesPerSecond => MessageCounter / GetUptime().TotalSeconds;
        public long TextChannels => Interlocked.Read(ref _textChannels);
        public long VoiceChannels => Interlocked.Read(ref _voiceChannels);
        public long Guilds => Interlocked.Read(ref _guilds);
        public long MessageCounter => Interlocked.Read(ref _messageCounter);
        public long CommandsRan => Interlocked.Read(ref _commandsRan);

        public void Initialize()
        {
            SocketGuild[] guilds = _client.Guilds.ToArray();
            _guilds = guilds.Length;
            _textChannels = guilds.Sum(guild => guild.Channels.Count(cx => cx is ITextChannel));
            _voiceChannels = guilds.Sum(guild => guild.Channels.Count(cx => cx is IVoiceChannel));
        }

        public TimeSpan GetUptime()
        {
            return DateTime.UtcNow - _started;
        }

        public string GetUptimeString(string separator)
        {
            return GetUptime().ToReadableString(separator);
        }
    }
}