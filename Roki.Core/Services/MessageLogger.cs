using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;

namespace Roki.Services
{
    public class MessageLogger : IRService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;

        public MessageLogger(DiscordSocketClient client, DbService db)
        {
            _client = client;
            _db = db;
        }

        public async Task StartLogging()
        {
            _client.MessageReceived += async message =>
            {
                using (var uow = _db.GetDbContext())
                {
                    if (!message.Author.IsBot)
                    {
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

                        var user = uow.DUsers.GetOrCreate(message.Author);
                        if (DateTime.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(5))
                        {
                            await uow.DUsers.UpdateXp(user).ConfigureAwait(false);
                        }

                        await uow.SaveChangesAsync().ConfigureAwait(false);
                    }
                }
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
            
            await Task.CompletedTask;
        }
    }
}