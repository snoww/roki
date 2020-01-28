using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Services;

namespace Roki.Modules.Administration.Services 
{
    public class AdministrationService : IRokiService
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
            using var uow = _db.GetDbContext();
            var channels = uow.Context.Channels.Where(c => c.Logging).ToList();
            foreach (var channel in channels.Select(ch => _client.GetChannel(ch.ChannelId) as ITextChannel))
            {
                if (channel == null) return;
                Console.Write($"{channel.Name}: ");
                IEnumerable<IMessage> messages;
                try
                {
                    messages = await channel.GetMessagesAsync(messageId, Direction.After, int.MaxValue).FlattenAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    continue;
                }
                Console.WriteLine(messages.Count());
                if (!messages.Any()) continue;
                
                var count = 0;
                foreach (var message in messages.Reverse())
                {
                    if (message.Author.IsBot) continue;
                    if (await uow.Messages.MessageExists(message.Id).ConfigureAwait(false)) continue;
                    await uow.Messages.AddMissingMessageAsync(message).ConfigureAwait(false);
                    count++;
                }
                Console.WriteLine($"{count} total messages added");
            }
        }
    }
}