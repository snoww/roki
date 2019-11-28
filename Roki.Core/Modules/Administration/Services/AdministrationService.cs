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

        public async Task<Dictionary<string, int>> FillMissingMessagesAsync(ulong messageId)
        {
            var guild = _client.Guilds.First(g => g.Id == _roki.Properties.PrimaryGuildId);
            var total = new Dictionary<string, int>();
            foreach (var channel in guild.TextChannels)
            {
                var messages = await channel.GetMessagesAsync(messageId, Direction.After, int.MaxValue).FlattenAsync().ConfigureAwait(false);
                var count = 1;
                using var uow = _db.GetDbContext();
                foreach (var message in messages.Reverse())
                {
                    if (message.Author.IsBot)
                    {
                        Console.WriteLine($"[{message.Id}] skipped");
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
                await uow.SaveChangesAsync().ConfigureAwait(false);
                total.Add(channel.Name, count);
            }

            return total;
        }
    }
}