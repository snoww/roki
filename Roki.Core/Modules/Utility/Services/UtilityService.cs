using System;
using System.Linq;
using System.Text;
using System.Threading;
using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using Roki.Services;

namespace Roki.Modules.Utility.Services
{
    public class UtilityService : IRokiService
    {
        private readonly DiscordSocketClient _client;
        private readonly IMongoService _mongo;
        private Timer _timer;
        private static readonly Random Rng = new Random();
        
        // temp hard coding values
        private const ulong GuildId = 125025699827417095;
        private static readonly ObjectId RainbowId = ObjectId.Parse("5db3150b03eb7230a1b5bb9d");
        private SocketGuild _guild;

        public UtilityService(DiscordSocketClient client, IMongoService mongo)
        {
            _client = client;
            _mongo = mongo;
            _guild = _client.GetGuild(GuildId);
            ColorChangeTimer();
        }

        private void ColorChangeTimer()
        {
            _timer = new Timer(ChangeRoleColor, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
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