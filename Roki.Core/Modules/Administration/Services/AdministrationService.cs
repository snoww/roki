using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NLog;
using Roki.Services;

namespace Roki.Modules.Administration.Services 
{
    public class AdministrationService : IRokiService
    {
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public AdministrationService(DbService db, DiscordSocketClient client)
        {
            _db = db;
            _client = client;
        }

        public async Task FillMissingMessagesAsync(ulong messageId)
        {
            using var uow = _db.GetDbContext();
            var channels = uow.Context.Channels.Where(c => c.Logging).ToArray();
            foreach (var channel in channels.Select(ch => _client.GetChannel(ch.ChannelId) as ITextChannel))
            {
                if (channel == null) return;
                
                try
                {
                    Logger.Info("Downloading messages from {channel}", channel.Name);
                    var messages = await channel.GetMessagesAsync(messageId, Direction.After, int.MaxValue).FlattenAsync().ConfigureAwait(false);
                    var messageList = messages.ToArray();
                    Logger.Info("{channel}: {amount} messages total", channel.Name, messageList.Length);
                    if (!messageList.Any()) continue;
                
                    var count = 0;
                    foreach (var message in messageList.Reverse())
                    {
                        if (message.Author.IsBot) continue;
                        if (await uow.Messages.MessageExists(message.Id).ConfigureAwait(false)) continue;
                        await uow.Messages.AddMissingMessageAsync(message).ConfigureAwait(false);
                        count++;
                    }

                    Logger.Info("{amount} total messages added", count);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }
    }
}