using Discord.Commands;
using Discord.WebSocket;

namespace Roki.Common.TypeReaders
{
    public abstract class RokiTypeReader<T> : TypeReader
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commandService;

        private RokiTypeReader()
        {
        }

        protected RokiTypeReader(DiscordSocketClient client, CommandService commandService)
        {
            _client = client;
            _commandService = commandService;
        }
    }
}