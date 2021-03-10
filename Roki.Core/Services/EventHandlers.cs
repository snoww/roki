using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Extensions;
using Roki.Modules.Xp.Common;
using Roki.Services.Database;
using Roki.Services.Database.Models;
using IDatabase = StackExchange.Redis.IDatabase;

namespace Roki.Services
{
    public class EventHandlers : IRokiService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IDatabase _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly DiscordSocketClient _client;
        private readonly ICurrencyService _currency;
        private readonly IConfigurationService _config;

        public EventHandlers(IServiceScopeFactory scopeFactory, DiscordSocketClient client, IRedisCache cache, ICurrencyService currency, IConfigurationService config)
        {
            _scopeFactory = scopeFactory;
            _client = client;
            _currency = currency;
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
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();

                await context.Messages.AddAsync(new Message(message.Id, channel.Id, channel.GuildId, message.Author.Id, message.Content, message.Reference?.MessageId.GetValueOrDefault(),
                    message.Attachments?.Select(x => x.Url).ToList()));
                await context.SaveChangesAsync();
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
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();

                Message message = await context.Messages.AsQueryable().SingleOrDefaultAsync(x => x.Id == after.Id);
                if (message == null)
                {
                    return;
                }

                message.Edits ??= new List<Edit>();
                message.Edits.Add(new Edit(after.Content, after.Attachments?.Select(x => x.Url).ToList(), DateTime.UtcNow));
                await context.SaveChangesAsync();
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
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();

                Message message = await context.Messages.AsQueryable().SingleOrDefaultAsync(x => x.Id == cache.Id);
                if (message == null)
                {
                    return;
                }

                message.Deleted = true;
                
                await context.SaveChangesAsync();
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
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                
                foreach (Cacheable<IMessage, ulong> cache in caches)
                {
                    if (cache.HasValue && cache.Value.Author.IsBot) continue;
                    Message message = await context.Messages.AsQueryable().SingleOrDefaultAsync(x => x.Id == cache.Id);
                    message.Deleted = true;
                }

                await context.SaveChangesAsync();
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
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                if (!await context.Guilds.AsQueryable().AnyAsync(x => x.Id == guild.Id))
                {
                    if (!await context.Users.AsQueryable().AnyAsync(x => x.Id == guild.OwnerId))
                    {
                        await context.Users.AddAsync(new User(guild.OwnerId, guild.Owner.Username, guild.Owner.Discriminator, guild.Owner.AvatarId));
                    }

                    await context.Guilds.AddAsync(new Guild(guild.Id, guild.Name, guild.IconId, guild.OwnerId)
                    {
                        GuildConfig = new GuildConfig()
                    });
                    
                    await context.UserData.AddRangeAsync(new UserData(Roki.BotId, guild.Id), new UserData(guild.OwnerId, guild.Id));
                    await context.Channels.AddRangeAsync(guild.TextChannels.Select(channel => new Channel(channel.Id, guild.Id, channel.Name) { ChannelConfig = new ChannelConfig()}).ToList());
                    await context.SaveChangesAsync();
                }
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
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                Guild dbGuild = await context.Guilds.AsQueryable().SingleOrDefaultAsync(x => x.Id == guild.Id);
                dbGuild.Available = false;
                await context.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }


        private Task GuildAvailable(SocketGuild guild)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                Guild dbGuild = await context.Guilds.AsQueryable().SingleAsync(x => x.Id == guild.Id);
                dbGuild.Available = true;
                await context.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task GuildUnavailable(SocketGuild guild)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                Guild dbGuild = await context.Guilds.AsQueryable().SingleAsync(x => x.Id == guild.Id);
                dbGuild.Available = false;
                await context.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task GuildUpdated(SocketGuild before, SocketGuild after)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                Guild dbGuild = await context.Guilds.AsQueryable().SingleOrDefaultAsync(x => x.Id == after.Id);
                if (dbGuild == null)
                {
                    if (!await context.Users.AsQueryable().AnyAsync(x => x.Id == after.OwnerId))
                    {
                        await context.Users.AddAsync(new User(after.OwnerId, after.Owner.Username, after.Owner.Discriminator, after.Owner.AvatarId));
                    }

                    await context.Guilds.AddAsync(new Guild(after.Id, after.Name, after.IconId, after.OwnerId)
                    {
                        GuildConfig = new GuildConfig()
                    });
                    await context.UserData.AddRangeAsync(new UserData(Roki.BotId, after.Id), new UserData(after.OwnerId, after.Id));
                    await context.Channels.AddRangeAsync(after.TextChannels.Select(channel => new Channel(channel.Id, after.Id, channel.Name) { ChannelConfig = new ChannelConfig()}).ToList());
                }
                else
                {
                    dbGuild.Name = after.Name;
                    dbGuild.Icon = after.IconId;
                    dbGuild.OwnerId = after.OwnerId;
                }

                await context.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task ChannelDestroyed(SocketChannel channel)
        {
            if (channel is not SocketTextChannel) return Task.CompletedTask;
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                Channel dbChannel = await context.Channels.AsQueryable().SingleOrDefaultAsync(x => x.Id == channel.Id);
                if (dbChannel != null)
                {
                    dbChannel.DeletedDate = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                }

            });
            return Task.CompletedTask;
        }

        private Task ChannelUpdated(SocketChannel before, SocketChannel after)
        {
            if (after is not SocketTextChannel textChannel) return Task.CompletedTask;
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                Channel dbChannel = await context.Channels.AsQueryable().SingleOrDefaultAsync(x => x.Id == after.Id);
                if (dbChannel == null)
                {
                    GuildConfig guildConfig = await _config.GetGuildConfigAsync(textChannel.Guild.Id);
                    await context.Channels.AddAsync(new Channel(textChannel.Id, textChannel.Guild.Id, textChannel.Name)
                    {
                        ChannelConfig = new ChannelConfig(guildConfig.Logging, guildConfig.CurrencyGen, guildConfig.XpGain)
                    });
                }
                else
                {
                    dbChannel.Name = textChannel.Name;
                }

                await context.SaveChangesAsync();
            });

            return Task.CompletedTask;
        }

        private Task ChannelCreated(SocketChannel channel)
        {
            if (channel is not SocketTextChannel textChannel) return Task.CompletedTask;
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(textChannel.Guild.Id);
                await context.Channels.AddAsync(new Channel(textChannel.Id, textChannel.Guild.Id, textChannel.Name)
                {
                    ChannelConfig = new ChannelConfig(guildConfig.Logging, guildConfig.CurrencyGen, guildConfig.XpGain)
                });
                await context.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task UserUpdated(SocketUser before, SocketUser after)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                User dbUser = await context.Users.AsQueryable().SingleOrDefaultAsync(x => x.Id == after.Id);
                if (dbUser == null)
                {
                    await context.Users.AddAsync(new User(after.Id, after.Username, after.Discriminator, after.AvatarId));
                }
                else
                {
                    dbUser.Id = after.Id;
                    dbUser.Username = after.Username;
                    dbUser.Discriminator = after.Discriminator;
                    dbUser.Avatar = after.AvatarId;
                }
                
                await context.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        private Task UserJoined(SocketGuildUser user)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                if (!await context.UserData.AsQueryable().AnyAsync(x => x.UserId == user.Id && x.GuildId == user.Guild.Id))
                {
                    UserData data;
                    if (await context.Users.AsNoTracking().AnyAsync(x => x.Id == user.Id))
                    {
                        data = new UserData(user.Id, user.Guild.Id);
                    }
                    else
                    {
                        data = new UserData(user.Id, user.Guild.Id)
                        {
                            User = new User(user.Username, user.Discriminator, user.AvatarId)
                        };
                    }
                
                    await context.UserData.AddAsync(data);
                    await context.SaveChangesAsync();
                }
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
            var context = scope.ServiceProvider.GetRequiredService<RokiContext>();

            GuildConfig guildConfig = await _config.GetGuildConfigAsync(channel.GuildId);

            UserData data = await context.UserData.AsQueryable().Where(x => x.UserId == message.Author.Id && x.GuildId == channel.GuildId).SingleOrDefaultAsync();
            if (data == null)
            {
                if (await context.Users.AsNoTracking().AnyAsync(x => x.Id == message.Author.Id))
                {
                    data = new UserData(message.Author.Id, channel.GuildId);
                }
                else
                {
                    data = new UserData(message.Author.Id, channel.GuildId)
                    {
                        User = new User(message.Author.Username, message.Author.Discriminator, message.Author.AvatarId)
                    };
                }
                
                await context.UserData.AddAsync(data);
            }

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

            data.LastXpGain = now;
            var newXp = new XpLevel(data.Xp);
            if (oldXp.Level == newXp.Level)
            {
                await context.SaveChangesAsync();
                return;
            }
            
            data.LastLevelUp = now;

            await SendNotification(data, message, newXp.Level).ConfigureAwait(false);
            List<XpReward> rewards = await context.XpRewards.AsNoTracking().Where(x => x.GuildId == channel.GuildId && x.Level == newXp.Level).ToListAsync();
            if (rewards.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (XpReward reward in rewards)
                {
                    string[] parsed = reward.Details.Split(';');
                    if (parsed.Length < 2)
                    {
                        continue;
                    }
                    
                    switch (parsed[0])
                    {
                        case "currency":
                        {
                            long amount = long.Parse(parsed[1]);
                            await _currency.AddCurrencyAsync(data.UserId, data.GuildId, channel.Id, message.Id, $"#{reward.Id} XP Level {reward.Level} Reward", amount);
                            sb.AppendLine($"+ `{amount:N0}` {guildConfig.CurrencyIcon}");
                            break;
                        }
                        case "role":
                        {
                            SocketRole role = (channel.Guild as SocketGuild)?.GetRole(ulong.Parse(parsed[1]));
                            if (role == null) continue;
                            var guildUser = (IGuildUser) message.Author;
                            await guildUser.AddRoleAsync(role).ConfigureAwait(false);
                            sb.AppendLine(reward.Description);
                            break;
                        }
                    }
                }

                IDMChannel dm = await message.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                try
                {
                    await dm.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle($"Level `{newXp.Level}` Rewards - {channel.Guild.Name}")
                            .WithDescription("Here are your rewards:\n" + sb))
                        .ConfigureAwait(false);
                    await dm.CloseAsync().ConfigureAwait(false);
                }
                catch
                {
                    // unable to send dm to user
                    // ignored
                }
            }

            await context.SaveChangesAsync();
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