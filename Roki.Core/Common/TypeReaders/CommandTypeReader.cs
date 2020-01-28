using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Roki.Services;

namespace Roki.Common.TypeReaders
{
    public class CommandTypeReader : RokiTypeReader<CommandInfo>
    {
        public CommandTypeReader(DiscordSocketClient client, CommandService commandService) : base (client, commandService)
        {
        }

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            var commands = services.GetService<CommandService>();
            var handler = services.GetService<CommandHandler>();
            input = input.ToUpperInvariant();
            var prefix = handler.DefaultPrefix;
            if (!input.StartsWith(prefix.ToUpperInvariant(), StringComparison.InvariantCulture))
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "No such command found"));

            input = input.Substring(prefix.Length);

            var command = commands.Commands.FirstOrDefault(c => c.Aliases.Select(a => a.ToUpperInvariant()).Contains(input));
            if (command == null)
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "No such command found"));

            return Task.FromResult(TypeReaderResult.FromSuccess(command));
        }
    }
}