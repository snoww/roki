using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MongoDB.Driver;
using NLog;
using Roki.Extensions;
using Roki.Modules.Xp.Common;
using Roki.Services.Database.Maps;
using StackExchange.Redis;

namespace Roki.Services
{
    public class EventHandlers : IRokiService
    {
        private readonly DiscordSocketClient _client;
        private readonly IDatabase _cache;

        private readonly IMongoCollection<Message> _messageCollection;
        private readonly IMongoCollection<Channel> _channelCollection;
        private readonly IMongoCollection<Guild> _guildCollection;
        private readonly IMongoCollection<User> _userCollection;
        private readonly IMongoCollection<Transaction> _transactionCollection;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public EventHandlers(IMongoDatabase database, DiscordSocketClient client, IRedisCache cache)
        {
            _client = client;
            _cache = cache.Redis.GetDatabase();

            _messageCollection = database.GetCollection<Message>("messages");
            _channelCollection = database.GetCollection<Channel>("channels");
            _guildCollection = database.GetCollection<Guild>("guilds");
            _userCollection = database.GetCollection<User>("users");
            _transactionCollection = database.GetCollection<Transaction>("transactions");
        }

        public async Task StartHandling()
        {
            _client.MessageReceived += MessageReceived;
            _client.MessageUpdated += MessageUpdated;
            _client.MessageDeleted += MessageDeleted;
            _client.MessagesBulkDeleted += MessagesBulkDeleted;
            _client.UserJoined += UserJoined;
            _client.UserUpdated += UserUpdated;
            _client.JoinedGuild += JoinedGuild;
            _client.LeftGuild += LeftGuild;
            _client.GuildUpdated += GuildUpdated;
            _client.GuildAvailable += GuildAvailable;
            _client.GuildUnavailable += GuildUnavailable;
            _client.ChannelCreated += ChannelCreated;
            _client.ChannelUpdated += ChannelUpdated;
            _client.ChannelDestroyed += ChannelDestroyed;
            _client.RoleUpdated += UpdateColor;
            _client.GuildMemberUpdated += UpdateColor;
            
            await Task.CompletedTask;
        }

        private Task MessageReceived(SocketMessage message)
        {
            if (message.Author.IsBot) 
                return Task.CompletedTask;

            UpdateXp(message).ConfigureAwait(false);
            
            var _ =  Task.Run(async () =>
            {
                if (!(await _channelCollection.Find(x => x.ChannelId == message.Channel.Id).FirstAsync()).Logging) 
                    return;
                
                await _messageCollection.InsertOneAsync(new Message
                {
                    MessageId = message.Id,
                    AuthorId = message.Author.Id,
                    ChannelId = message.Channel.Id,
                    GuildId = message.Channel is ITextChannel channelId ? channelId.GuildId : (ulong?) null,
                    Content = message.Content,
                    Attachments = message.Attachments?.Select(a => a.Url).ToList(),
                    Timestamp = message.Timestamp
                }).ConfigureAwait(false);
            });

            return Task.CompletedTask;
        }

        private Task MessageUpdated(Cacheable<IMessage, ulong> cache, SocketMessage after, ISocketMessageChannel channel)
        {
            if (after.Author.IsBot) return Task.CompletedTask;
            if (string.IsNullOrWhiteSpace(after.Author.Username)) return Task.CompletedTask;
            if (after.EditedTimestamp == null) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                if (after.Channel is SocketTextChannel textChannel)
                    if (!(await _channelCollection.Find(x => x.ChannelId == textChannel.Id).FirstAsync()).Logging) 
                        return;
                
                var update = Builders<Message>.Update.Push(m => m.Edits, new Edit
                {
                    Content = after.Content,
                    Attachments = after.Attachments.Select(m => m.Url).ToList(),
                    EditedTimestamp = after.EditedTimestamp ?? after.Timestamp
                });

                await _messageCollection.FindOneAndUpdateAsync(m => m.MessageId == after.Id, update).ConfigureAwait(false);
            });
                
            return Task.CompletedTask;
        }

        private Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            if (cache.HasValue && cache.Value.Author.IsBot) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                if (channel is SocketTextChannel textChannel)
                    if (!(await _channelCollection.Find(x => x.ChannelId == textChannel.Id).FirstAsync()).Logging) 
                        return;

                var update = Builders<Message>.Update.Set(m => m.IsDeleted, true);

                await _messageCollection.FindOneAndUpdateAsync(m => m.MessageId == cache.Id, update).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> caches, ISocketMessageChannel channel)
        {
            var _ = Task.Run(async () =>
            {
                if (channel is SocketTextChannel textChannel)
                    if (!(await _channelCollection.Find(x => x.ChannelId == textChannel.Id).FirstAsync()).Logging) 
                        return;
                
                foreach (var cache in caches)
                {
                    if (cache.HasValue && cache.Value.Author.IsBot) continue;
                    
                    var update = Builders<Message>.Update.Set(m => m.IsDeleted, true);
                    
                    await _messageCollection.FindOneAndUpdateAsync(m => m.MessageId == cache.Id, update).ConfigureAwait(false);
                }
            });
            
            return Task.CompletedTask;
        }
        
        private Task GuildAvailable(SocketGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                var update = Builders<Guild>.Update.Set(x => x.Available, true);
                
                await _guildCollection.FindOneAndUpdateAsync(x => x.GuildId == guild.Id, update).ConfigureAwait(false);
            });
            
            return Task.CompletedTask;
        }

        private Task GuildUnavailable(SocketGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                var update = Builders<Guild>.Update.Set(x => x.Available, false);
                
                await _guildCollection.FindOneAndUpdateAsync(x => x.GuildId == guild.Id, update).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task GuildUpdated(SocketGuild before, SocketGuild after)
        {
            var _ = Task.Run(async () =>
            {
                var updateGuild = Builders<Guild>.Update.Set(g => g.Name, after.Name)
                    .Set(g => g.IconId, after.IconId)
                    .Set(g => g.ChannelCount, after.Channels.Count)
                    .Set(g => g.MemberCount, after.MemberCount)
                    .Set(g => g.EmoteCount, after.Emotes.Count)
                    .Set(g => g.OwnerId, after.OwnerId)
                    .Set(g => g.RegionId, after.VoiceRegionId);

                await _guildCollection.FindOneAndUpdateAsync(g => g.GuildId == after.Id, updateGuild).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task ChannelDestroyed(SocketChannel channel)
        {
            if (!(channel is SocketTextChannel textChannel)) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                var updateChannel = Builders<Channel>.Update.Set(c => c.IsDeleted, true);
                
                await _channelCollection.FindOneAndUpdateAsync(c => c.ChannelId == textChannel.Id, updateChannel).ConfigureAwait(false);
            });

            return Task.CompletedTask;
        }

        private Task ChannelUpdated(SocketChannel before, SocketChannel after)
        {
            if (!(before is SocketTextChannel textChannel)) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                var updateChannel = Builders<Channel>.Update.Set(c => c.Name, textChannel.Name)
                    .Set(c => c.GuildId, textChannel.Guild.Id)
                    .Set(c => c.IsNsfw, textChannel.IsNsfw);
                
                await _channelCollection.FindOneAndUpdateAsync(c => c.ChannelId == textChannel.Id, updateChannel).ConfigureAwait(false);
            });
            
            return Task.CompletedTask;
        }

        private Task ChannelCreated(SocketChannel channel)
        {
            if (!(channel is SocketTextChannel textChannel)) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                await _channelCollection.InsertOneAsync(new Channel
                {
                    ChannelId = textChannel.Id,
                    Name = textChannel.Name,
                    GuildId = textChannel.Guild.Id,
                    IsNsfw = textChannel.IsNsfw
                }).ConfigureAwait(false);
            });
            
            return Task.CompletedTask;
        }

        private Task JoinedGuild(SocketGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                if (guild == null) 
                    return;
                
                Logger.Info("Joined server: {guild} [{guildid}]", guild.Name, guild.Id);
                await guild.DownloadUsersAsync().ConfigureAwait(false);
                
                var updateGuild = Builders<Guild>.Update.Set(g => g.GuildId, guild.Id)
                    .Set(g => g.Name, guild.Name)
                    .Set(g => g.IconId, guild.IconId)
                    .Set(g => g.ChannelCount, guild.Channels.Count)
                    .Set(g => g.MemberCount, guild.MemberCount)
                    .Set(g => g.EmoteCount, guild.Emotes.Count)
                    .Set(g => g.OwnerId, guild.OwnerId)
                    .Set(g => g.RegionId, guild.VoiceRegionId)
                    .Set(g => g.CreatedAt, guild.CreatedAt);

                await _guildCollection.FindOneAndUpdateAsync<Guild>(g => g.GuildId == guild.Id, updateGuild,
                    new FindOneAndUpdateOptions<Guild> {IsUpsert = true});
                
                var users = guild.Users;
                foreach (var user in users)
                {
                    var updateUser = Builders<User>.Update.Set(u => u.Id, user.Id)
                        .Set(u => u.Username, user.Username)
                        .Set(u => u.Discriminator, int.Parse(user.Discriminator))
                        .Set(u => u.AvatarId, user.AvatarId);

                    await _userCollection.FindOneAndUpdateAsync<User>(u => u.Id == user.Id, updateUser,
                        new FindOneAndUpdateOptions<User> {IsUpsert = true}).ConfigureAwait(false);
                }
            });
            
            return Task.CompletedTask;
        }

        private Task LeftGuild(SocketGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                if (guild == null) 
                    return;
                
                Logger.Info("Left server: {guild} [{guildid}]", guild.Name, guild.Id);
                var updateGuild = Builders<Guild>.Update.Set(c => c.Available, false);
                
                await _guildCollection.FindOneAndUpdateAsync(g => g.GuildId == guild.Id, updateGuild).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task UserUpdated(SocketUser before, SocketUser after)
        {
            var _ = Task.Run(async () =>
            {
                var updateUser = Builders<User>.Update.Set(u => u.Id, after.Id)
                    .Set(u => u.Username, after.Username)
                    .Set(u => u.Discriminator, int.Parse(after.Discriminator))
                    .Set(u => u.AvatarId, after.AvatarId);

                await _userCollection.FindOneAndUpdateAsync(u => u.Id == after.Id, updateUser).ConfigureAwait(false);
            });
            
            return Task.CompletedTask;
        }

        private Task UserJoined(SocketGuildUser user)
        {
            var _ = Task.Run(async () =>
            {
                var updateUser = Builders<User>.Update.Set(u => u.Id, user.Id)
                    .Set(u => u.Username, user.Username)
                    .Set(u => u.Discriminator, int.Parse(user.Discriminator))
                    .Set(u => u.AvatarId, user.AvatarId);

                await _userCollection.FindOneAndUpdateAsync<User>(u => u.Id == user.Id, updateUser,
                    new FindOneAndUpdateOptions<User>{ReturnDocument = ReturnDocument.After, IsUpsert = true}).ConfigureAwait(false);

            });
            
            return Task.CompletedTask;
        }

        private Task UpdateColor(SocketGuildUser before, SocketGuildUser after)
        {
            var _ = Task.Run(async () =>
            {
                if (after.Id != _client.CurrentUser.Id)
                    return;

                var currentTopRole = after.Roles.OrderByDescending(r => r.Position).FirstOrDefault();
                if (currentTopRole == null)
                    return;

                await _cache.StringSetAsync($"color:{after.Guild.Id}", currentTopRole.Color.RawValue);
            });

            return Task.CompletedTask;
        }

        private Task UpdateColor(SocketRole before, SocketRole after)
        {
            var _ = Task.Run(async () =>
            {
                var guild = after.Guild;
                var currentGuildUser = guild.CurrentUser;
                if (!currentGuildUser.Roles.Contains(after)) 
                    return;
                var currentTopRole = currentGuildUser.Roles.OrderByDescending(r => r.Position).FirstOrDefault();
                if (currentTopRole == null || currentTopRole != after)
                    return;

                await _cache.StringSetAsync($"color:{guild.Id}", currentTopRole.Color.RawValue);
            });

            return Task.CompletedTask;
        }

        private Task UpdateXp(SocketMessage message)
        {
            var _ = Task.Run(async () =>
            {
                var user = await _userCollection.Find(x => x.Id == message.Author.Id).FirstOrDefaultAsync().ConfigureAwait(false);
                // temp
                var doubleXp = user.Subscriptions.Any(u => u.Id.Equals(Guid.Parse("44ccdd38-98d3-3312-8e22-4c0159ab028f")));
                var fastXp = user.Subscriptions.Any(u => u.Id.Equals(Guid.Parse("4ae529bc-7205-70b9-8e22-4c0159ab2c80")));
                
                var updateXp = Builders<User>.Update.Inc(u => u.Xp,
                    doubleXp ? Roki.Properties.XpPerMessage * 2 : Roki.Properties.XpPerMessage);
                
                var oldLevel = new XpLevel(user.Xp);
                var newXp = 0;
                if (fastXp)
                {
                    if (DateTimeOffset.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(Roki.Properties.XpFastCooldown))
                    {
                        newXp = await _userCollection.FindOneAndUpdateAsync(u => u.Id == user.Id, updateXp,
                                new FindOneAndUpdateOptions<User, int> {ReturnDocument = ReturnDocument.After})
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    if (DateTimeOffset.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(Roki.Properties.XpCooldown))
                    {
                        newXp = await _userCollection.FindOneAndUpdateAsync(u => u.Id == user.Id, updateXp,
                                new FindOneAndUpdateOptions<User, int> {ReturnDocument = ReturnDocument.After})
                            .ConfigureAwait(false);
                    }
                }
                
                if (newXp == 0)
                    return;

                var newLevel = new XpLevel(newXp);
                
                if (newLevel.Level > oldLevel.Level)
                {
                    await SendNotification(user, message, newLevel.Level).ConfigureAwait(false);
                    var textChannel = (SocketTextChannel) message.Channel;
                    var rewards = (await _guildCollection.Find(g => g.GuildId == textChannel.Guild.Id).FirstAsync()).XpRewards;
                    if (rewards != null && rewards.Count != 0)
                    {
                        foreach (var reward in rewards)
                        {
                            if (reward.Type == "currency")
                            {
                                var amount = int.Parse(reward.Reward);
                                var updateCurrency = Builders<User>.Update.Inc(u => u.Currency, amount);
                                await _userCollection.UpdateOneAsync(u => u.Id == user.Id, updateCurrency).ConfigureAwait(false);
                                await _transactionCollection.InsertOneAsync(new Transaction
                                {
                                    Amount = amount,
                                    Reason = "XP Level Up Reward",
                                    To = user.Id,
                                    From = 0,
                                    GuildId = textChannel.Guild.Id,
                                    ChannelId = textChannel.Id,
                                    MessageId = message.Id
                                });
                            }
                            else
                            {
                                var role = textChannel.Guild.GetRole(ulong.Parse(reward.Reward));
                                if (role == null) continue;
                                var guildUser = (IGuildUser) message.Author;
                                await guildUser.AddRoleAsync(role).ConfigureAwait(false);
                            }
                        }

                        var dm = await message.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                        try
                        {
                            await dm.EmbedAsync(new EmbedBuilder().WithOkColor()
                                    .WithTitle($"Level `{newLevel.Level}` Rewards")
                                    .WithDescription("Here are your rewards:\n" + string.Join("\n", rewards
                                                         .Select(r => r.Type == "currency"
                                                             ? $"+ `{int.Parse(r.Reward):N0}` {Roki.Properties.CurrencyIcon}"
                                                             : $"+ {textChannel.Guild.GetRole(ulong.Parse(r.Reward)).Name ?? "N/A"} Role"))))
                                .ConfigureAwait(false);
                            await dm.CloseAsync().ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // unable to send dm to user
                            // ignored
                        }
                    }
                }
            });
            
            return Task.CompletedTask;
        }

        private static async Task SendNotification(User user, SocketMessage msg, int level)
        {
            if (user.Notification.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
            }
            else if (user.Notification.Equals("dm", StringComparison.OrdinalIgnoreCase))
            {
                var dm = await msg.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                await dm.SendMessageAsync($"Congratulations {msg.Author.Mention}! You've reached Level {level}").ConfigureAwait(false);
            }
            else if (user.Notification.Equals("server", StringComparison.OrdinalIgnoreCase))
            {
                await msg.Channel.SendMessageAsync($"Congratulations {msg.Author.Mention}! You've reached Level {level}").ConfigureAwait(false);
            }
        }
    }
}