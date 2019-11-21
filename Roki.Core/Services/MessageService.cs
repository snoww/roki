using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private readonly Roki _roki;

        public MessageService(DiscordSocketClient client, DbService db, Roki roki)
        {
            _client = client;
            _db = db;
            _roki = roki;
        }

        public async Task StartService()
        {
            _client.MessageReceived += MessageReceived;
            _client.MessageUpdated += MessageUpdated;
            _client.MessageDeleted += MessageDeleted;
            _client.MessagesBulkDeleted += MessagesBulkDeleted;
            
            await Task.CompletedTask;
        }

        private async Task MessageReceived(SocketMessage message)
        {
            if (message.Author.IsBot) return;
            using (var uow = _db.GetDbContext())
            {
                var user = await uow.DUsers.GetOrCreate(message.Author).ConfigureAwait(false);
                var doubleXp = uow.Subscriptions.DoubleXpIsActive(message.Author.Id);
                var fastXp = uow.Subscriptions.FastXpIsActive(message.Author.Id);
                if (fastXp)
                {
                    if (DateTimeOffset.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(_roki.Properties.XpFastCooldown))
                        await uow.DUsers.UpdateXp(user, message, doubleXp).ConfigureAwait(false);
                }
                else
                {
                    if (DateTimeOffset.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(_roki.Properties.XpCooldown))
                        await uow.DUsers.UpdateXp(user, message, doubleXp).ConfigureAwait(false);
                }

                string content;
                if (!string.IsNullOrWhiteSpace(message.Content) && message.Attachments.Count == 0)
                    content = message.Content;
                else if (!string.IsNullOrWhiteSpace(message.Content) && message.Attachments.Count > 0)
                    content = message.Content + "\n" + string.Join("\n", message.Attachments.Select(a => a.Url));
                else if (message.Attachments.Count > 0)
                    content = string.Join("\n", message.Attachments.Select(a => a.Url));
                else
                    content = "";
                
                uow.DMessages.Add(new DMessage
                {
                    AuthorId = message.Author.Id,
                    Author = message.Author.Username,
                    ChannelId = message.Channel.Id,
                    Channel = message.Channel.Name,
                    GuildId = message.Channel is ITextChannel chId ? chId.GuildId : (ulong?) null,
                    Guild = message.Channel is ITextChannel ch ? ch.Guild.Name : null,
                    MessageId = message.Id,
                    Content = content,
                    EditedTimestamp = message.EditedTimestamp?.ToUniversalTime(),
                    Timestamp = message.Timestamp.ToUniversalTime()
                });

                await uow.SaveChangesAsync().ConfigureAwait(false);
            }

            await Task.CompletedTask;
        }

        private async Task MessageUpdated(Cacheable<IMessage, ulong> cache, SocketMessage after, ISocketMessageChannel channel)
        {
            if (after.Author.IsBot)
                return;
            using (var uow = _db.GetDbContext())
            {
                string content;
                if (!string.IsNullOrWhiteSpace(after.Content) && after.Attachments.Count == 0)
                    content = after.Content;
                else if (!string.IsNullOrWhiteSpace(after.Content) && after.Attachments.Count > 0)
                    content = after.Content + "\n" + string.Join("\n", after.Attachments.Select(a => a.Url));
                else if (after.Attachments.Count > 0)
                    content = string.Join("\n", after.Attachments.Select(a => a.Url));
                else
                    content = "";
                uow.DMessages.Add(new DMessage
                {
                    AuthorId = after.Author.Id,
                    Author = after.Author.Username,
                    ChannelId = after.Channel.Id,
                    Channel = after.Channel.Name,
                    GuildId = after.Channel is ITextChannel chId ? chId.GuildId : (ulong?) null,
                    Guild = after.Channel is ITextChannel ch ? ch.Guild.Name : null,
                    MessageId = after.Id,
                    Content = content,
                    EditedTimestamp = after.EditedTimestamp?.ToUniversalTime(),
                    Timestamp = after.Timestamp.ToUniversalTime()
                });
                    
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
                
            await Task.CompletedTask;
        }

        private async Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            await Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                if (!cache.HasValue)
                {
                    uow.DMessages.MessageDeleted(cache.Id);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    return;
                }

                if (cache.Value.Author.IsBot)
                    return;
                uow.DMessages.MessageDeleted(cache.Value.Id);

                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
            
            await Task.CompletedTask;
        }

        private async Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> caches, ISocketMessageChannel channel)
        {
            foreach (var cache in caches)
            {
                using var uow = _db.GetDbContext();
                if (!cache.HasValue)
                {
                    uow.DMessages.MessageDeleted(cache.Id);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    continue;
                }
                if (cache.Value.Author.IsBot)
                    return;
                uow.DMessages.MessageDeleted(cache.Value.Id);
                
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
            
            await Task.CompletedTask;
        }
    }
}