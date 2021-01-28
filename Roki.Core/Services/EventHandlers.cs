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
using Roki.Services.Database;
using Roki.Services.Database.Maps;
using StackExchange.Redis;

namespace Roki.Services
{
    public class EventHandlers : IRokiService
    {
        private static readonly ObjectId DoubleXpId = ObjectId.Parse("5db772de03eb7230a1b5bba1");
        private static readonly ObjectId FastXpId = ObjectId.Parse("5dbc2dd103eb7230a1b5bba5");

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IDatabase _cache;
        private readonly DiscordSocketClient _client;
        private readonly IMongoContext _context;
        private readonly IConfigurationService _config;

        public EventHandlers(IMongoService service, DiscordSocketClient client, IRedisCache cache, IConfigurationService config)
        {
            _context = service.Context;
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
                var textChannel = message.Channel as ITextChannel;
                bool isDmChannel = message.Channel is IDMChannel;
                if (!isDmChannel && textChannel != null)
                {
                    await UpdateXp(message, textChannel).ConfigureAwait(false);
                }
                if (isDmChannel || !await _config.LoggingEnabled(textChannel))
                {
                    return;
                }

                await _context.AddMessageAsync(message).ConfigureAwait(false);
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
                if (channel is IDMChannel || !await _config.LoggingEnabled(channel as ITextChannel))
                {
                    return;
                }

                await _context.AddMessageEditAsync(after).ConfigureAwait(false);
            });

            return Task.CompletedTask;
        }

        private Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            if (cache.HasValue && cache.Value.Author.IsBot) return Task.CompletedTask;
            Task _ = Task.Run(async () =>
            {
                if (channel is IDMChannel || !await _config.LoggingEnabled(channel as ITextChannel))
                {
                    return;
                }

                await _context.MessageDeletedAsync(cache.Id).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> caches, ISocketMessageChannel channel)
        {
            Task _ = Task.Run(async () =>
            {
                if (channel is IDMChannel || !await _config.LoggingEnabled(channel as ITextChannel))
                {
                    return;
                }

                foreach (Cacheable<IMessage, ulong> cache in caches)
                {
                    if (cache.HasValue && cache.Value.Author.IsBot) continue;

                    await _context.MessageDeletedAsync(cache.Id).ConfigureAwait(false);
                }
            });

            return Task.CompletedTask;
        }

        private Task GuildAvailable(SocketGuild guild)
        {
            Task _ = Task.Run(async () => { await _context.ChangeGuildAvailabilityAsync(guild, true).ConfigureAwait(false); });
            return Task.CompletedTask;
        }

        private Task GuildUnavailable(SocketGuild guild)
        {
            Task _ = Task.Run(async () => { await _context.ChangeGuildAvailabilityAsync(guild, false).ConfigureAwait(false); });
            return Task.CompletedTask;
        }

        private Task GuildUpdated(SocketGuild before, SocketGuild after)
        {
            Task _ = Task.Run(async () => { await _context.UpdateGuildAsync(after).ConfigureAwait(false); });
            return Task.CompletedTask;
        }

        private Task ChannelDestroyed(SocketChannel channel)
        {
            if (!(channel is SocketTextChannel textChannel)) return Task.CompletedTask;
            Task _ = Task.Run(async () => { await _context.DeleteChannelAsync(textChannel).ConfigureAwait(false); });
            return Task.CompletedTask;
        }

        private Task ChannelUpdated(SocketChannel before, SocketChannel after)
        {
            if (!(before is SocketTextChannel textChannel)) return Task.CompletedTask;
            Task _ = Task.Run(async () => { await _context.UpdateChannelAsync(textChannel).ConfigureAwait(false); });

            return Task.CompletedTask;
        }

        private Task ChannelCreated(SocketChannel channel)
        {
            if (!(channel is SocketTextChannel textChannel)) return Task.CompletedTask;
            Task _ = Task.Run(async () => { await _context.GetOrAddChannelAsync(textChannel).ConfigureAwait(false); });
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

                await _context.GetOrAddGuildAsync(guild).ConfigureAwait(false);

                foreach (SocketGuildChannel channel in guild.Channels)
                {
                    if (!(channel is SocketTextChannel textChannel))
                    {
                        continue;
                    }

                    await _context.GetOrAddChannelAsync(textChannel).ConfigureAwait(false);
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

                await _context.ChangeGuildAvailabilityAsync(guild, false);
            });
            return Task.CompletedTask;
        }

        private Task UserUpdated(SocketUser before, SocketUser after)
        {
            Task _ = Task.Run(async () => { await _context.UpdateUserAsync(after).ConfigureAwait(false); });
            return Task.CompletedTask;
        }

        private Task UserJoined(SocketGuildUser user)
        {
            Task _ = Task.Run(async () => { await _context.GetOrAddUserAsync(user, user.Guild.Id.ToString()); });
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

        private async Task UpdateXp(SocketMessage message, ITextChannel channel)
        {
            if (!await _config.XpGainEnabled(channel))
            {
                return;
            }
            
            GuildConfig guildConfig = await _config.GetGuildConfigAsync(channel.GuildId);

            var guildId = channel.GuildId.ToString();
            User user = await _context.GetOrAddUserAsync(message.Author, guildId).ConfigureAwait(false);

            // temp
            bool doubleXp = user.Data[guildId].Subscriptions.ContainsKey(DoubleXpId);
            bool fastXp = user.Data[guildId].Subscriptions.ContainsKey(FastXpId);

            var oldXp = new XpLevel(user.Data[guildId].Xp);
            XpLevel newXp = doubleXp ? oldXp.AddXp(guildConfig.XpPerMessage * 2) : oldXp.AddXp(guildConfig.XpPerMessage);
            bool levelUp = oldXp.Level < newXp.Level;

            DateTime now = DateTime.UtcNow;
            if (fastXp && now - user.Data[guildId].LastXpGain >= TimeSpan.FromMinutes(guildConfig.XpFastCooldown))
            {
                await _context.UpdateUserXpAsync(user, guildId, now, newXp.TotalXp - oldXp.TotalXp, levelUp).ConfigureAwait(false);
                goto levelUp;
            }

            if (DateTime.UtcNow - user.Data[guildId].LastXpGain >= TimeSpan.FromMinutes(guildConfig.XpCooldown))
            {
                await _context.UpdateUserXpAsync(user, guildId, now, newXp.TotalXp - oldXp.TotalXp, levelUp).ConfigureAwait(false);
                goto levelUp;
            }

            return;
            
            levelUp:
            await SendNotification(user, guildId, message, newXp.Level).ConfigureAwait(false);
            Dictionary<ObjectId, XpReward> rewards = (await _context.GetOrAddGuildAsync(channel.Guild as SocketGuild).ConfigureAwait(false)).XpRewards;
            if (rewards != null && rewards.Count != 0)
            {
                foreach ((ObjectId _, XpReward reward) in rewards)
                {
                    if (reward.Type == "currency")
                    {
                        int amount = int.Parse(reward.Reward);
                        await _context.UpdateUserCurrencyAsync(user, guildId, amount).ConfigureAwait(false);
                        await _context.AddTransaction(new Transaction
                        {
                            Amount = amount,
                            Reason = "XP Level Up Reward",
                            To = user.Id,
                            From = 0,
                            GuildId = channel.GuildId,
                            ChannelId = channel.Id,
                            MessageId = message.Id
                        });
                    }
                    else
                    {
                        SocketRole role = (channel.Guild as SocketGuild)?.GetRole(ulong.Parse(reward.Reward));
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
                                .Select(r => r.Value.Type == "currency"
                                    ? $"+ `{int.Parse(r.Value.Reward):N0}` {guildConfig.CurrencyIcon}"
                                    : $"+ {r.Value.Reward}"))))
                        .ConfigureAwait(false);
                    await dm.CloseAsync().ConfigureAwait(false);
                }
                catch
                {
                    // unable to send dm to user
                    // ignored
                }
            }
        }

        private static async Task SendNotification(User user, string guildId, SocketMessage msg, int level)
        {
            if (user.Data[guildId].Notification.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (user.Data[guildId].Notification.Equals("dm", StringComparison.OrdinalIgnoreCase))
            {
                IDMChannel dm = await msg.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                try
                {
                    await dm.EmbedAsync(new EmbedBuilder().WithDynamicColor(ulong.Parse(guildId)).WithDescription($"Congratulations {msg.Author.Mention}! You've reached Level {level}"));
                }
                catch
                {
                    // user has disabled dms
                }
            }
            else if (user.Data[guildId].Notification.Equals("server", StringComparison.OrdinalIgnoreCase))
            {
                await msg.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ulong.Parse(guildId)).WithDescription($"Congratulations {msg.Author.Mention}! You've reached Level {level}"));
            }
        }
    }
}