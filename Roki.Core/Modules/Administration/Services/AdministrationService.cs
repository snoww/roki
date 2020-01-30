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
        private readonly Logger _log;

        public AdministrationService(DbService db, DiscordSocketClient client)
        {
            _db = db;
            _client = client;
            _log = LogManager.GetCurrentClassLogger();
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
                    _log.Info("Downloading messages from {0}", channel.Name);
                    var messages = await channel.GetMessagesAsync(messageId, Direction.After, int.MaxValue).FlattenAsync().ConfigureAwait(false);
                    var messageList = messages.ToArray();
                    _log.Info("{0}: {1} messages total", channel.Name, messageList.Length);
                    if (!messageList.Any()) continue;
                
                    var count = 0;
                    foreach (var message in messageList.Reverse())
                    {
                        if (message.Author.IsBot) continue;
                        if (await uow.Messages.MessageExists(message.Id).ConfigureAwait(false)) continue;
                        await uow.Messages.AddMissingMessageAsync(message).ConfigureAwait(false);
                        count++;
                    }

                    _log.Info("{0} total messages added", count);
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }
        }
    }
}