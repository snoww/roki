using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Core.Services.Database;
using Roki.Core.Services.Database.Models;

namespace Roki.Services
{
    public class MessageService : IRService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;
        private readonly CommandHandler _cmdHandler;
        public ConcurrentDictionary<ulong, DateTime> LastGenerations { get; } = new ConcurrentDictionary<ulong, DateTime>();

        public MessageService(DiscordSocketClient client, DbService db, CommandHandler cmdHandler)
        {
            _client = client;
            _db = db;
            _cmdHandler = cmdHandler;
        }

        public async Task StartService()
        {
            _client.MessageReceived += async message =>
            {
                using (var uow = _db.GetDbContext())
                {
                    if (!message.Author.IsBot)
                    {
                        await CurrencyGeneration(message).ConfigureAwait(false);
                        
                        var user = uow.DUsers.GetOrCreate(message.Author);
                        
                        if (DateTime.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(5))
                            await uow.DUsers.UpdateXp(user, message).ConfigureAwait(false);

                        uow.DMessages.Add(new DMessage
                        {
                            AuthorId = message.Author.Id,
                            Author = message.Author.Username,
                            ChannelId = message.Channel.Id,
                            Channel = message.Channel.Name,
                            GuildId = message.Channel is ITextChannel chId ? chId.GuildId : (ulong?) null,
                            Guild = message.Channel is ITextChannel ch ? ch.Guild.Name : null,
                            MessageId = message.Id,
                            Content = message.Content,
                            EditedTimestamp = message.EditedTimestamp?.UtcDateTime,
                            Timestamp = message.Timestamp.UtcDateTime
                        });

                        await uow.SaveChangesAsync().ConfigureAwait(false);
                    }
                }

                await Task.CompletedTask;
            };
            
            _client.MessageUpdated += async (_, after, __) =>
            {
                if (_.Value.Author.IsBot)
                    return;
                using (var uow = _db.GetDbContext())
                {
                    uow.DMessages.Add(new DMessage
                    {
                        AuthorId = after.Author.Id,
                        Author = after.Author.Username,
                        ChannelId = after.Channel.Id,
                        Channel = after.Channel.Name,
                        GuildId = after.Channel is ITextChannel chId ? chId.GuildId : (ulong?) null,
                        Guild = after.Channel is ITextChannel ch ? ch.Guild.Name : null,
                        MessageId = after.Id,
                        Content = after.Content,
                        EditedTimestamp = after.EditedTimestamp?.UtcDateTime,
                        Timestamp = after.Timestamp.UtcDateTime
                    });
                    
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }
                
                await Task.CompletedTask;
            };

            _client.MessageDeleted += async (_, channel) =>
            {
                if (_.Value.Author.IsBot)
                    return;
                using (var uow = _db.GetDbContext())
                {
                    uow.DMessages.MessageDeleted(_.Value.Id);
                    
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }

                await Task.CompletedTask;
            };
            
            await Task.CompletedTask;
        }

        private async Task CurrencyGeneration(SocketMessage message)
        {
            if (!(message is SocketUserMessage msg))
                return;
            if (!(message.Channel is ITextChannel channel))
                return;
            // TODO add ignored channels, change constants to database values

            var lastGeneration = LastGenerations.GetOrAdd(channel.Id, DateTime.MinValue);
            var rng = new Random();
            
            if (DateTime.UtcNow - TimeSpan.FromMinutes(5) < lastGeneration)
                return;

            var num = rng.Next(1, 101) + 1.5 * 100;
            if (num > 100 && LastGenerations.TryUpdate(channel.Id, DateTime.UtcNow, lastGeneration))
            {
                var drop = 1;
                var dropMax = 5;
                
                if (dropMax != null && dropMax > drop)
                    drop = new Random().Next(drop, dropMax + 1);

                if (drop > 0)
                {
                    var prefix = _cmdHandler.DefaultPrefix;
                    var toSend = drop == 1
                        ? $"A random stone appeared! Type `{prefix}pick` to pick it up."
                        : $"Some random stones appeared! Type `{prefix}pick` to pick them up.";
                    // TODO add images to send with drop
                    await channel.SendMessageAsync(toSend).ConfigureAwait(false);
                }
            }
        }
    }
}