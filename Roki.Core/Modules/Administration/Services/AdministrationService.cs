using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Extensions;

namespace Roki.Modules.Administration.Services 
{
    public class AdministrationService : IRService
    {
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private readonly Roki _roki;

        public AdministrationService(DbService db, DiscordSocketClient client, Roki roki)
        {
            _db = db;
            _client = client;
            _roki = roki;
        }

        public async Task FillMissingMessagesAsync(ulong messageId)
        {
            var guild = _client.Guilds.First(g => g.Id == _roki.Properties.PrimaryGuildId);
            var total = new Dictionary<string, int>();
            foreach (var channel in guild.TextChannels)
            {
                Console.Write($"{channel.Name}: ");
                var messages = await channel.GetMessagesAsync(messageId, Direction.After, int.MaxValue).FlattenAsync().ConfigureAwait(false);
                Console.WriteLine(messages.Count());
                if (!messages.Any()) continue;
                var count = 0;
                using var uow = _db.GetDbContext();
                foreach (var message in messages.Reverse())
                {
                    if (message.Author.IsBot)
                    {
                        continue;
                    }
                    if (await uow.Messages.MessageExists(message.Id).ConfigureAwait(false))
                    {
                        continue;
                    }
                    await uow.Messages.AddMissingMessageAsync(message).ConfigureAwait(false);
                    Console.WriteLine($"[{message.Id}] added");
                    count++;
                }
                Console.WriteLine($"{count} total messages added");
            }
        }
    }
}