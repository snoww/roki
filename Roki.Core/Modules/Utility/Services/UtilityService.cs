using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Modules.Utility.Common;
using Roki.Services;

namespace Roki.Modules.Utility.Services
{
    public class UtilityService : IRokiService
    {
        // temp hard coding values
        private const ulong DefaultGuildId = 125025699827417095;
        private static readonly Random Rng = new();

        private readonly DiscordSocketClient _client;
        private readonly SocketGuild _guild;
        private Timer _status;

        public UtilityService(DiscordSocketClient client)
        {
            _client = client;
            _guild = _client.GetGuild(DefaultGuildId);
            // StartTimers();
        }

        // todo custom statuses
        private void StartTimers()
        {
            // _status = new Timer(RotateStatus, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
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