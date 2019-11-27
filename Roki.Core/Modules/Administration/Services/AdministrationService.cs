using System;
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

        public AdministrationService(DbService db)
        {
            _db = db;
        }

        public async Task FillMissingMessagesAsync(ICommandContext ctx, ulong messageId)
        {
            if (!(ctx.Channel is SocketTextChannel channel)) return;
            var messages = await channel.GetMessagesAsync(messageId, Direction.After, 10000).FlattenAsync().ConfigureAwait(false);
            using var uow = _db.GetDbContext();
            var count = 1;
            foreach (var message in messages)
            {
                if (message.Author.IsBot) continue;
                if (await uow.Messages.MessageExists(message.Id).ConfigureAwait(false))
                {
                    continue;
                }
                await uow.Messages.AddMissingMessageAsync(message).ConfigureAwait(false);
                count++;
            }
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await ctx.Channel.SendMessageAsync($"{count} messages have been added");
        }
    }
}