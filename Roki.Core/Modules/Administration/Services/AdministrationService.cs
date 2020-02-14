using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MongoDB.Driver;
using NLog;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Administration.Services 
{
    public class AdministrationService : IRokiService
    {
        private readonly IMongoService _mongo;
        private readonly DiscordSocketClient _client;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public AdministrationService(DiscordSocketClient client, IMongoService mongo)
        {
            _client = client;
            _mongo = mongo;
        }

        public async Task FillMissingMessagesAsync(ulong messageId)
        {
            using var channels = await _mongo.Context.ChannelCollection.FindAsync(x => x.Logging).ConfigureAwait(false);
            while (await channels.MoveNextAsync())
            {
                foreach (var ch in channels.Current)
                {
                    var channel = _client.GetChannel(ch.Id) as ITextChannel;
                    if (channel == null)
                        continue;

                    try
                    {
                        Logger.Info("Downloading messages from {channel}", channel.Name);
                        var messages = await channel.GetMessagesAsync(messageId, Direction.After, int.MaxValue).FlattenAsync().ConfigureAwait(false);
                        var messageList = messages.ToArray();
                        Logger.Info("{channel}: {amount} messages total", channel.Name, messageList.Length);
                        if (!messageList.Any()) 
                            continue;
            
                        var count = 0;
                        foreach (var message in messageList)
                        {
                            if (message.Author.IsBot) 
                                continue;
                            var findMessage = await _mongo.Context.MessageCollection.Find(m => m.Id == message.Id).FirstOrDefaultAsync();
                            if (findMessage != null)
                                continue;
                            
                            var mongoMessage = new Message
                            {
                                AuthorId = message.Id,
                                ChannelId = message.Channel.Id,
                                Content = message.Content,
                                GuildId = message.Channel is ITextChannel chId ? chId.GuildId : (ulong?) null,
                                Id = message.Id,
                                Timestamp = message.Timestamp.UtcDateTime
                            };

                            if (message.EditedTimestamp != null)
                            {
                                mongoMessage.Edits.Add(new Edit{Content = message.Content, EditedTimestamp = message.EditedTimestamp.Value.UtcDateTime});
                            }
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
}