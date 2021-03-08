using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Extensions;
using Roki.Modules.Xp.Common;
using Roki.Services.Database;
using Roki.Services.Database.Models;
using StackExchange.Redis;

namespace Roki.Services
{
    public class EventHandlers : IRokiService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IDatabase _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly DiscordSocketClient _client;
        private readonly IConfigurationService _config;

        public EventHandlers(IServiceScopeFactory scopeFactory, DiscordSocketClient client, IRedisCache cache, IConfigurationService config)
        {
            _scopeFactory = scopeFactory;
            _client = client;
            _config = config;
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
            {
                return Task.CompletedTask;
            }


            Task _ = Task.Run(async () =>
            {
                if (message.Channel is not IGuildChannel channel || !await _config.LoggingEnabled(channel.Id))
                {
                    return;
                }

                await UpdateXp(message, channel).ConfigureAwait(false);

                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();

                await service.Context.Messages.AddAsync(new Message
                {
                    Id = message.Id,
                    AuthorId = message.Author.Id,
                    ChannelId = channel.Id,
                    GuildId = channel.GuildId,
                    Content = message.Content,
                    RepliedTo = message.Reference.MessageId.GetValueOrDefault(),
                    Attachments = message.Attachments?.Select(x => x.Url).ToList()
                });
                await service.SaveChangesAsync();
            });

            return Task.CompletedTask;
        }

        private Task MessageUpdated(Cacheable<IMessage, ulong> cache, SocketMessage after, ISocketMessageChannel channel)
        {
            if (after.Author.IsBot) return Task.CompletedTask;
            if (string.IsNullOrWhiteSpace(after.Author.Username)) return Task.CompletedTask;
            if (after.EditedTimestamp == null) return Task.CompletedTask;
            Task _ = Task.Run(async () =>
            {
                if (channel is not IGuildChannel || !await _config.LoggingEnabled(channel.Id))
                {
                    return;
                }
                
                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();

                Message message = await service.Context.Messages.SingleOrDefaultAsync(x => x.Id == after.Id);
                if (message == null)
                {
                    return;
                }

                message.Edits ??= new List<Edit>();
                message.Edits.Add(new Edit
                {
                    Content = after.Content,
                    Attachments = after.Attachments?.Select(x => x.Url).ToList(),
                    Timestamp = DateTime.UtcNow
                });
                await service.SaveChangesAsync();
            });

            return Task.CompletedTask;
        }

        private Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            if (cache.HasValue && cache.Value.Author.IsBot) return Task.CompletedTask;
            Task _ = Task.Run(async () =>
            {
                if (channel is not IGuildChannel || !await _config.LoggingEnabled(channel.Id))
                {
                    return;
                }
                
                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();

                Message message = await service.Context.Messages.SingleOrDefaultAsync(x => x.Id == cache.Id);
                if (message == null)
                {
                    return;
                }

                message.Deleted = true;
                
                await service.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> caches, ISocketMessageChannel channel)
        {
            Task _ = Task.Run(async () =>
            {
                if (channel is not IGuildChannel || !await _config.LoggingEnabled(channel.Id))
                {
                    return;
                }
                
                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();
                
                foreach (Cacheable<IMessage, ulong> cache in caches)
                {
                    if (cache.HasValue && cache.Value.Author.IsBot) continue;
                    Message message = await service.Context.Messages.SingleOrDefaultAsync(x => x.Id == cache.Id);
                    message.Deleted = true;
                }

                await service.SaveChangesAsync();
            });

            return Task.CompletedTask;
        }

        private Task GuildAvailable(SocketGuild guild)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();
                Guild dbGuild = await service.Context.Guilds.SingleAsync(x => x.Id == guild.Id);
                dbGuild.Available = true;
                await service.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task GuildUnavailable(SocketGuild guild)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();
                Guild dbGuild = await service.Context.Guilds.SingleAsync(x => x.Id == guild.Id);
                dbGuild.Available = false;
                await service.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task GuildUpdated(SocketGuild before, SocketGuild after)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();
                Guild dbGuild = await service.Context.Guilds.SingleOrDefaultAsync(x => x.Id == after.Id);
                if (dbGuild == null)
                {
                    await service.AddGuildIfNotExistsAsync(after);
                    foreach (SocketTextChannel channel in after.TextChannels)
                    {
                        await service.AddChannelIfNotExistsAsync(channel);
                    }
                }
                else
                {
                    dbGuild.Name = after.Name;
                    dbGuild.Icon = after.IconId;
                    dbGuild.OwnerId = after.OwnerId;
                }

                await service.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task ChannelDestroyed(SocketChannel channel)
        {
            if (channel is not SocketTextChannel) return Task.CompletedTask;
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();
                Channel dbChannel = await service.Context.Channels.SingleOrDefaultAsync(x => x.Id == channel.Id);
                if (dbChannel != null)
                {
                    dbChannel.DeletedDate = DateTime.UtcNow;
                }

                await service.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task ChannelUpdated(SocketChannel before, SocketChannel after)
        {
            if (after is not SocketTextChannel textChannel) return Task.CompletedTask;
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();
                Channel dbChannel = await service.Context.Channels.SingleOrDefaultAsync(x => x.Id == after.Id);
                if (dbChannel == null)
                {
                    await service.AddChannelIfNotExistsAsync(textChannel);
                }
                else
                {
                    dbChannel.Name = textChannel.Name;
                }

                await service.SaveChangesAsync();
            });

            return Task.CompletedTask;
        }

        private Task ChannelCreated(SocketChannel channel)
        {
            if (channel is not SocketTextChannel textChannel) return Task.CompletedTask;
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();
                await service.AddChannelIfNotExistsAsync(textChannel);
                await service.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task JoinedGuild(SocketGuild guild)
        {
            Task _ = Task.Run(async () =>
            {
                if (guild == null)
                {
                    return;
                }

                Logger.Info("Joined server: {guild} [{guildid}]", guild.Name, guild.Id);

                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();
                await service.AddGuildIfNotExistsAsync(guild);
                foreach (SocketTextChannel channel in guild.TextChannels)
                {
                    await service.AddChannelIfNotExistsAsync(channel);
                }
                await service.SaveChangesAsync();
            });

            return Task.CompletedTask;
        }

        private Task LeftGuild(SocketGuild guild)
        {
            Task _ = Task.Run(async () =>
            {
                if (guild == null)
                {
                    return;
                }

                Logger.Info("Left server: {guild} [{guildid}]", guild.Name, guild.Id);

                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();
                Guild dbGuild = await service.Context.Guilds.SingleOrDefaultAsync(x => x.Id == guild.Id);
                dbGuild.Available = false;
                await service.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task UserUpdated(SocketUser before, SocketUser after)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();
                User dbUser = await service.Context.Users.SingleOrDefaultAsync(x => x.Id == after.Id);
                if (dbUser == null)
                {
                    await service.Context.Users.AddAsync(new User
                    {
                        Id = after.Id,
                        Username = after.Username,
                        Discriminator = after.Discriminator,
                        Avatar = after.AvatarId,
                    });
                }
                else
                {
                    dbUser.Id = after.Id;
                    dbUser.Username = after.Username;
                    dbUser.Discriminator = after.Discriminator;
                    dbUser.Avatar = after.AvatarId;
                }
                
                await service.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task UserJoined(SocketGuildUser user)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();
                await service.GetOrAddUserAsync(user, user.Guild.Id);
                // add user data separately in case user is already in another server with bot
                await service.GetOrCreateUserDataAsync(user.Id, user.Guild.Id);
                await service.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task UpdateColor(SocketGuildUser before, SocketGuildUser after)
        {
            Task _ = Task.Run(async () =>
            {
                if (after.Id != _client.CurrentUser.Id)
                {
                    return;
                }

                SocketRole currentTopRole = after.Roles.OrderByDescending(r => r.Position).FirstOrDefault();
                if (currentTopRole == null || currentTopRole.IsEveryone)
                {
                    return;
                }

                await _cache.StringSetAsync($"color:{after.Guild.Id}", currentTopRole.Color.RawValue);
            });

            return Task.CompletedTask;
        }

        private Task UpdateColor(SocketRole before, SocketRole after)
        {
            Task _ = Task.Run(async () =>
            {
                SocketGuild guild = after.Guild;
                SocketGuildUser currentGuildUser = guild.CurrentUser;
                if (!currentGuildUser.Roles.Contains(after))
                {
                    return;
                }

                SocketRole currentTopRole = currentGuildUser.Roles.OrderByDescending(r => r.Position).FirstOrDefault();
                if (currentTopRole == null || currentTopRole != after)
                {
                    return;
                }

                await _cache.StringSetAsync($"color:{guild.Id}", currentTopRole.Color.RawValue);
            });

            return Task.CompletedTask;
        }

        private async Task UpdateXp(SocketMessage message, IGuildChannel channel)
        {
            if (!await _config.XpGainEnabled(channel.Id))
            {
                return;
            }
            
            using IServiceScope scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRokiDbService>();

            GuildConfig guildConfig = await _config.GetGuildConfigAsync(channel.GuildId);

            await service.AddUserIfNotExistsAsync(message.Author);
            UserData data = await service.GetOrCreateUserDataAsync(message.Author.Id, channel.GuildId);

            // temp
            // these are always false
            bool doubleXp = data.Xp < 0;
            bool fastXp = data.Xp < 0;

            var oldXp = new XpLevel(data.Xp);

            DateTime now = DateTime.UtcNow;
            if (fastXp && now - data.LastXpGain >= TimeSpan.FromSeconds(guildConfig.XpFastCooldown))
            {
                data.Xp += doubleXp ? guildConfig.XpPerMessage * 2 : guildConfig.XpPerMessage;
            }
            else if (DateTime.UtcNow - data.LastXpGain >= TimeSpan.FromSeconds(guildConfig.XpCooldown))
            {
                data.Xp += doubleXp ? guildConfig.XpPerMessage * 2 : guildConfig.XpPerMessage;
            }
            else
            {
                return;
            }

            data.LastLevelUp = now;
            var newXp = new XpLevel(data.Xp);
            if (oldXp.Level == newXp.Level)
            {
                await service.SaveChangesAsync();
                return;
            }
            
            data.LastLevelUp = now;

            await SendNotification(data, message, newXp.Level).ConfigureAwait(false);
            List<XpReward> rewards = await service.GetXpRewardsAsync(channel.GuildId, newXp.Level);
            if (rewards.Count > 0)
            {
                foreach (XpReward reward in rewards)
                {
                    if (reward.Type == "currency")
                    {
                        long amount = long.Parse(reward.Description);
                        // todo cache
                        data.Currency += amount;
                        await service.Context.Transactions.AddAsync(new Transaction
                        {
                            Amount = amount,
                            Description = "XP Level Up Reward",
                            Recipient = data.UserId,
                            Sender = Roki.BotId,
                            GuildId = channel.GuildId,
                            ChannelId = channel.Id,
                            MessageId = message.Id
                        });
                    }
                    else
                    {
                        SocketRole role = (channel.Guild as SocketGuild)?.GetRole(ulong.Parse(reward.Description));
                        if (role == null) continue;
                        var guildUser = (IGuildUser) message.Author;
                        await guildUser.AddRoleAsync(role).ConfigureAwait(false);
                    }
                }

                IDMChannel dm = await message.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                try
                {
                    await dm.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle($"Level `{newXp.Level}` Rewards")
                            .WithDescription("Here are your rewards:\n" + string.Join("\n", rewards
                                .Select(r => r.Type == "currency"
                                    ? $"+ `{long.Parse(r.Description):N0}` {guildConfig.CurrencyIcon}"
                                    : $"+ {r.Description}"))))
                        .ConfigureAwait(false);
                    await dm.CloseAsync().ConfigureAwait(false);
                }
                catch
                {
                    // unable to send dm to user
                    // ignored
                }
            }

            await service.SaveChangesAsync();
        }

        private static async Task SendNotification(UserData data, SocketMessage msg, int level)
        {
            switch (data.NotificationLocation)
            {
                case 0:
                    return;
                case 1:
                    await msg.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(data.GuildId).WithDescription($"Congratulations {msg.Author.Mention}! You've reached Level {level}"));
                    break;
                case 2:
                    IDMChannel dm = await msg.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                    try
                    {
                        await dm.EmbedAsync(new EmbedBuilder().WithDynamicColor(data.GuildId).WithDescription($"Congratulations {msg.Author.Mention}! You've reached Level {level}"));
                    }
                    catch
                    {
                        // user has disabled dms
                    }
                    break;
            }
        }
    }
}