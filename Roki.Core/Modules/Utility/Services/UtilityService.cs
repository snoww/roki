using System;
using System.Linq;
using System.Threading;
using Discord;
using Discord.WebSocket;
using Roki.Services;

namespace Roki.Modules.Utility.Services
{
    public class UtilityService : IRokiService
    {
        private readonly DiscordSocketClient _client;
        private Timer _timer;
        private const ulong GuildId = 125025699827417095;
        private readonly Random _rng = new Random();

        public UtilityService(DiscordSocketClient client)
        {
            _client = client; 
            ColorChangeTimer();
        }

        private void ColorChangeTimer()
        {
            _timer = new Timer(ChangeRoleColor, null, TimeSpan.Zero, TimeSpan.FromMinutes(30));
        }
        
        private async void ChangeRoleColor(object state)
        {
            var guild = _client.GetGuild(GuildId);
            var roles = guild.Roles.Where(r => r.Name.Contains("rainbow", StringComparison.OrdinalIgnoreCase)).ToList();
            try
            {
                foreach (var role in roles)
                {
                    await role.ModifyAsync(r => r.Color = new Color(_rng.Next(0, 256), _rng.Next(0, 256), _rng.Next(0, 256))).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public string Uwulate(string message)
        {
            var result = string.Empty;
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
                        result += "W";
                        break;
                    case 'l':
                    case 'r':
                        result += "w";
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
                                result += "yo";
                                break;
                            default:
                                result += currChar;
                                break;
                        }
                        break;
                    default:
                        result += currChar;
                        break;
                }
            }

            result += " uwu";
            return result;
        }
    }
}