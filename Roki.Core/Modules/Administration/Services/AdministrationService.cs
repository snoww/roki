using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Core.Services;

namespace Roki.Modules.Administration.Services 
{
    public class AdministrationService : IRService
    {
        private readonly DbService _db;

        public AdministrationService(DbService db)
        {
            _db = db;
        }

        public async Task FillMissingMessagesAsync(ICommandContext ctx, ulong messageId)
        {
            if (!(ctx.Channel is SocketTextChannel channel)) return;
            var messages = await channel.GetMessagesAsync(messageId, Direction.After, 10000).FlattenAsync().ConfigureAwait(false);
            using var uow = _db.GetDbContext();
            int i = 1;
            foreach (var message in messages)
            {
                if (message.Author.IsBot) continue;
                i++;
                Console.Write(i + ": ");
                if (await uow.Messages.MessageExists(message.Id).ConfigureAwait(false))
                {
                    Console.WriteLine($"[{message.Id}] exists");
                    continue;
                }
                await uow.Messages.AddToTempTableAsync(message).ConfigureAwait(false);
                Console.WriteLine($"[{message.Id}] added");
            }
            await uow.Messages.MoveToTempTableAsync(messageId).ConfigureAwait(false);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}