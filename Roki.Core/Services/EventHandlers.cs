using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
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
        private readonly IMongoContext _context;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public EventHandlers(IMongoService service, DiscordSocketClient client, IRedisCache cache)
        {
            _context = service.Context;
            _client = client;
            _cache = cache.Redis.GetDatabase();
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
                if (!_context.IsLoggingEnabled(message.Channel as ITextChannel)) 
                    return;

                await _context.AddMessageAsync(message).ConfigureAwait(false);
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
                if (!_context.IsLoggingEnabled(after.Channel as ITextChannel)) 
                    return;

                await _context.AddMessageEditAsync(after).ConfigureAwait(false);
            });
                
            return Task.CompletedTask;
        }

        private Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            if (cache.HasValue && cache.Value.Author.IsBot) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                if (!_context.IsLoggingEnabled(channel as ITextChannel)) 
                    return;

                await _context.MessageDeletedAsync(cache.Id).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> caches, ISocketMessageChannel channel)
        {
            var _ = Task.Run(async () =>
            {
                if (!_context.IsLoggingEnabled(channel as ITextChannel)) 
                    return;
                
                foreach (var cache in caches)
                {
                    if (cache.HasValue && cache.Value.Author.IsBot) continue;
                    
                    await _context.MessageDeletedAsync(cache.Id).ConfigureAwait(false);
                }
            });
            
            return Task.CompletedTask;
        }
        
        private Task GuildAvailable(SocketGuild guild)
        {
            var _ = Task.Run(async () => { await _context.ChangeGuildAvailabilityAsync(guild, true).ConfigureAwait(false); });
            return Task.CompletedTask;
        }

        private Task GuildUnavailable(SocketGuild guild)
        {
            var _ = Task.Run(async () => { await _context.ChangeGuildAvailabilityAsync(guild, false).ConfigureAwait(false); });
            return Task.CompletedTask;
        }

        private Task GuildUpdated(SocketGuild before, SocketGuild after)
        {
            var _ = Task.Run(async () => { await _context.UpdateGuildAsync(after).ConfigureAwait(false); });
            return Task.CompletedTask;
        }

        private Task ChannelDestroyed(SocketChannel channel)
        {
            if (!(channel is SocketTextChannel textChannel)) return Task.CompletedTask;
            var _ = Task.Run(async () => { await _context.DeleteChannelAsync(textChannel).ConfigureAwait(false); });
            return Task.CompletedTask;
        }

        private Task ChannelUpdated(SocketChannel before, SocketChannel after)
        {
            if (!(before is SocketTextChannel textChannel)) return Task.CompletedTask;
            var _ = Task.Run(async () => { await _context.UpdateChannelAsync(textChannel).ConfigureAwait(false); });
            
            return Task.CompletedTask;
        }

        private Task ChannelCreated(SocketChannel channel)
        {
            if (!(channel is SocketTextChannel textChannel)) return Task.CompletedTask;
            var _ = Task.Run(async () => { await _context.GetOrAddChannelAsync(textChannel).ConfigureAwait(false); });
            return Task.CompletedTask;
        }

        private Task JoinedGuild(SocketGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                if (guild == null) 
                    return;
                
                Logger.Info("Joined server: {guild} [{guildid}]", guild.Name, guild.Id);

                await _context.GetOrAddGuildAsync(guild).ConfigureAwait(false);
                
                foreach (var user in guild.Users)
                {
                    await _context.GetOrAddUserAsync(user).ConfigureAwait(false);
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

                await _context.ChangeGuildAvailabilityAsync(guild, false);
            });
            return Task.CompletedTask;
        }

        private Task UserUpdated(SocketUser before, SocketUser after)
        {
            var _ = Task.Run(async () => { await _context.UpdateUserAsync(after).ConfigureAwait(false); });
            return Task.CompletedTask;
        }

        private Task UserJoined(SocketGuildUser user)
        {
            var _ = Task.Run(async () => { await _context.GetOrAddUserAsync(user); });
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
                var user = await _context.GetOrAddUserAsync(message.Author).ConfigureAwait(false);
                // temp
                var doubleXp = user.Subscriptions.Any(x => x.Id == ObjectId.Empty);
                var fastXp = user.Subscriptions.Any(x => x.Id == ObjectId.Empty);

                var oldLevel = new XpLevel(user.Xp);
                var newXp = 0;
                if (fastXp)
                {
                    if (DateTimeOffset.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(Roki.Properties.XpFastCooldown))
                    {
                        newXp = await _context.UpdateUserXpAsync(user, doubleXp).ConfigureAwait(false);
                    }
                }
                else
                {
                    if (DateTimeOffset.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(Roki.Properties.XpCooldown))
                    {
                        newXp = await _context.UpdateUserXpAsync(user, doubleXp).ConfigureAwait(false);
                    }
                }
                
                if (newXp == 0)
                    return;

                var newLevel = new XpLevel(newXp);
                
                if (newLevel.Level > oldLevel.Level)
                {
                    await SendNotification(user, message, newLevel.Level).ConfigureAwait(false);
                    var textChannel = (SocketTextChannel) message.Channel;
                    var rewards = (await _context.GetOrAddGuildAsync(textChannel.Guild).ConfigureAwait(false)).XpRewards;
                    if (rewards != null && rewards.Count != 0)
                    {
                        foreach (var reward in rewards)
                        {
                            if (reward.Type == "currency")
                            {
                                var amount = int.Parse(reward.Reward);
                                await _context.UpdateUserCurrencyAsync(user, amount).ConfigureAwait(false);
                                await _context.AddTransaction(new Transaction
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