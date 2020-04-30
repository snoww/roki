using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using Roki.Modules.Utility.Common;
using Roki.Services;

namespace Roki.Modules.Utility.Services
{
    public class UtilityService : IRokiService
    {
        private readonly DiscordSocketClient _client;
        private readonly IMongoService _mongo;
        private Timer _timer;
        private Timer _status;
        private static readonly Random Rng = new Random();
        
        // temp hard coding values
        private const ulong DefaultGuildId = 125025699827417095;
        private static readonly ObjectId RainbowId = ObjectId.Parse("5db3150b03eb7230a1b5bb9d");
        private SocketGuild _guild;

        public UtilityService(DiscordSocketClient client, IMongoService mongo)
        {
            _client = client;
            _mongo = mongo;
            _guild = _client.GetGuild(DefaultGuildId);
            StartTimers();
        }

        private void StartTimers()
        {
            _timer = new Timer(ChangeRoleColor, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
            _status = new Timer(RotateStatus, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
        }
        
        private async void ChangeRoleColor(object state)
        {
            // if no users has rainbow subscription, do not update
            var users = await _mongo.Context.UserCollection.Find(x => x.Subscriptions.Any(y => y.Id == RainbowId)).Limit(1).CountDocumentsAsync()
                .ConfigureAwait(false);
            if (users == 0)
                return;
            
            var roles = _guild.Roles.Where(r => r.Name.Contains("rainbow", StringComparison.OrdinalIgnoreCase)).ToList();
            try
            {
                foreach (var role in roles)
                {
                    await role.ModifyAsync(r => r.Color = new Color(Rng.Next(256), Rng.Next(256), Rng.Next(256))).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async void RotateStatus(object state)
        {
            var random = Rng.Next(3);
            if (random == 0)
            {
                await _client.SetGameAsync(await GetMCStatus().ConfigureAwait(false), null, ActivityType.CustomStatus).ConfigureAwait(false);
            }
            else if (random == 1)
            {
                await _client.SetGameAsync("with Thermonuclear Warheads");
            }
            else if (random == 2)
            {
                await _client.SetGameAsync("Ivor's personal project video", null, ActivityType.Watching);
            }
            
        }

        private async Task<string> GetMCStatus()
        {
            var server = new MinecraftServer("mc.roki.sh");
            var status = await server.GetStatus().ConfigureAwait(false);
            if (status.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    using var json = JsonDocument.Parse(status);
                    var players = json.RootElement.GetProperty("players");
                    return $"MC Server: {players.GetProperty("online").GetInt32()}/{players.GetProperty("max").GetInt32()} online";
                }
                catch (Exception)
                {
                    return "Server Offline";
                }
            }

            return status;
        }

        public string Uwulate(string message)
        {
            var result = new StringBuilder();
            for (int i = 0; i < message.Length; i++)
            {
                var currChar = message[i];
                var preChar = '\0';
                if (i > 0)
                    preChar = message[i - 1];

                switch (currChar)
                {
                    case 'L':
                    case 'R':
                        result.Append("W");
                        break;
                    case 'l':
                    case 'r':
                        result.Append("w");
                        break;
                    // special case
                    case 'o':
                    case 'O':
                        switch (preChar)
                        {
                            case 'n':
                            case 'N':
                            case 'm':
                            case 'M':
                                result.Append("yo");
                                break;
                            default:
                                result.Append(currChar);
                                break;
                        }
                        break;
                    default:
                        result.Append(currChar);
                        break;
                }
            }

            result.Append(" uwu");
            return result.ToString();
        }
    }
}