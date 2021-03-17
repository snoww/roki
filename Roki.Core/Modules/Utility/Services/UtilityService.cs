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
        public UtilityService()
        {
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