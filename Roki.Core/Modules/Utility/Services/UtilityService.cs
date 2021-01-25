using System;
using System.Collections.Generic;
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
        // temp hard coding values
        private const ulong DefaultGuildId = 125025699827417095;
        private static readonly Random Rng = new();
        private static readonly ObjectId RainbowId = ObjectId.Parse("5db3150b03eb7230a1b5bb9d");

        private readonly DiscordSocketClient _client;
        private readonly SocketGuild _guild;
        private readonly IMongoService _mongo;
        private Timer _status;
        private Timer _timer;

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
            long users = await _mongo.Context.UserCollection.Find(x => x.Subscriptions.Any(y => y.Id == RainbowId)).Limit(1).CountDocumentsAsync()
                .ConfigureAwait(false);
            if (users == 0)
            {
                return;
            }

            List<SocketRole> roles = _guild.Roles.Where(r => r.Name.Contains("rainbow", StringComparison.OrdinalIgnoreCase)).ToList();
            try
            {
                foreach (SocketRole role in roles) await role.ModifyAsync(r => r.Color = new Color(Rng.Next(256), Rng.Next(256), Rng.Next(256))).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async void RotateStatus(object state)
        {
            int random = Rng.Next(3);
            if (random == 0)
            {
                Task _ = Task.Run(async () =>
                {
                    for (var i = 0; i < 10; i++)
                    {
                        await _client.SetGameAsync(await GetMCStatus().ConfigureAwait(false)).ConfigureAwait(false);
                        await Task.Delay(TimeSpan.FromSeconds(55)).ConfigureAwait(false);
                    }
                });
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

        private static async Task<string> GetMCStatus()
        {
            var server = new MinecraftServer("localhost");
            string status = await server.GetStatus().ConfigureAwait(false);
            if (status.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    using JsonDocument json = JsonDocument.Parse(status);
                    JsonElement players = json.RootElement.GetProperty("players");
                    return $"with {players.GetProperty("online").GetInt32()}/{players.GetProperty("max").GetInt32()} on Minecraft Server";
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
            for (var i = 0; i < message.Length; i++)
            {
                char currChar = message[i];
                var preChar = '\0';
                if (i > 0)
                {
                    preChar = message[i - 1];
                }

                switch (currChar)
                {
                    case 'L':
                    case 'R':
                        result.Append('W');
                        break;
                    case 'l':
                    case 'r':
                        result.Append('w');
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