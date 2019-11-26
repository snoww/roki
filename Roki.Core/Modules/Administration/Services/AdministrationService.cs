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
            var channel = ctx.Channel as SocketTextChannel;
            var rawMessages = channel?.GetMessagesAsync(messageId, Direction.After, int.MaxValue);
            var messages = await rawMessages.FlattenAsync().ConfigureAwait(false);
            using var uow = _db.GetDbContext();
            foreach (var message in messages)
            {
                if (await uow.Messages.MessageExists(message.Id).ConfigureAwait(false)) continue;
                await uow.Messages.AddToTempTableAsync(message).ConfigureAwait(false);
            }

            await uow.Messages.MoveToTempTableAsync(messageId).ConfigureAwait(false);
            await uow.Messages.MoveBackToMessagesAsync(messageId).ConfigureAwait(false);
        }
    }
}