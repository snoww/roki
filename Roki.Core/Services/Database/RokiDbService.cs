using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Roki.Services.Database.Models;

namespace Roki.Services.Database
{
    public interface IRokiDbService
    {
        RokiContext Context { get; }
        int SaveChanges();
        Task<int> SaveChangesAsync();

        #region Users

        Task AddUserIfNotExistsAsync(IUser user);
        Task<User> GetOrAddUserAsync(IUser user, ulong guildId);
        Task<UserData> GetOrCreateUserDataAsync(ulong userId, ulong guildId);
        Task<int> GetUserXpRankAsync(ulong userId, ulong guildId);
        Task<List<Subscription>> GetUserSubscriptionsAsync(ulong userId, ulong guildId);

        #endregion

        #region Guilds

        Task AddGuildIfNotExistsAsync(IGuild guild);
        Task<GuildConfig> GetGuildConfigAsync(ulong guildId);

        #endregion

        #region Channels

        Task AddChannelIfNotExistsAsync(ITextChannel channel);

        #endregion

        #region Currency

        Task<long> GetUserCurrencyAsync(ulong userId, ulong guildId);
        Task<decimal> GetUserInvestingAsync(ulong userId, ulong guildId);
        Task<bool> RemoveCurrencyAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount);
        Task<bool> RemoveCurrencyAsync(UserData data, UserData botData, ulong guildId, ulong channelId, ulong messageId, string reason, long amount);
        Task AddCurrencyAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount);
        Task AddCurrencyAsync(UserData data, UserData botData, ulong guildId, ulong channelId, ulong messageId, string reason, long amount);
        Task<bool> TransferCurrencyAsync(ulong senderId, ulong recipientId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount);

        #endregion

        #region XpRewards

        Task<List<XpReward>> GetXpRewardsAsync(ulong guildId, int level);

        #endregion

    }
    
    public class RokiDbService : IRokiDbService
    {
        public RokiContext Context { get; }
        public int SaveChanges() => Context.SaveChanges();
        public async Task<int> SaveChangesAsync() => await Context.SaveChangesAsync();

        public RokiDbService(RokiContext context)
        {
            Context = context;
        }

        public async Task AddUserIfNotExistsAsync(IUser user)
        {
            if (await Context.Users.AsNoTracking().AsAsyncEnumerable().AnyAsync(x => x.Id == user.Id))
            {
                return;
            }

            var dbUser = new User
            {
                Id = user.Id,
                Username = user.Username,
                Discriminator = user.Discriminator,
                Avatar = user.AvatarId
            };
            
            await Context.Users.AddAsync(dbUser);
        }

        public async Task<User> GetOrAddUserAsync(IUser user, ulong guildId)
        {
            User dbUser = await Context.Users.AsAsyncEnumerable().SingleOrDefaultAsync(x => x.Id == user.Id);
            if (dbUser != null)
            {
                return dbUser;
            }

            dbUser = new User
            {
                Id = user.Id,
                Username = user.Username,
                Discriminator = user.Discriminator,
                Avatar = user.AvatarId,
            };

            GuildConfig guildConfig = await GetGuildConfigAsync(guildId);
            if (guildConfig != null)
            {
                dbUser.UserData.Add(new UserData
                {
                    GuildId = guildId,
                    NotificationLocation = guildConfig.NotificationLocation,
                    Currency = guildConfig.CurrencyDefault,
                    Investing = guildConfig.InvestingDefault
                });
            }
            else
            {
                dbUser.UserData.Add(new UserData
                {
                    GuildId = guildId
                });
            }

            await Context.Users.AddAsync(dbUser);
            return dbUser;
        }

        public async Task<UserData> GetOrCreateUserDataAsync(ulong userId, ulong guildId)
        {
            UserData data = await Context.UserData.AsAsyncEnumerable().SingleOrDefaultAsync(x => x.UserId == userId && x.GuildId == guildId);
            if (data != null)
            {
                return data;
            }
            
            GuildConfig guildConfig = await GetGuildConfigAsync(guildId);
            if (guildConfig != null)
            {
                data = new UserData
                {
                    UserId = userId,
                    GuildId = guildId,
                    NotificationLocation = guildConfig.NotificationLocation,
                    Currency = guildConfig.CurrencyDefault,
                    Investing = guildConfig.InvestingDefault
                };
            }
            else
            {
                data = new UserData
                {
                    UserId = userId,
                    GuildId = guildId
                };
            }

            return data;
        }

        public async Task<int> GetUserXpRankAsync(ulong userId, ulong guildId)
        {
            var conn = (NpgsqlConnection) Context.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("select r.rank from (select d.uid, d.xp, rank() over (order by d.xp desc) rank from user_data d where d.guild_id = (@guild_id)) r where r.uid = (@uid)", conn);
            cmd.Parameters.AddWithValue("guild_id", guildId);
            cmd.Parameters.AddWithValue("uid", userId);
            await cmd.PrepareAsync();
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            return reader.GetInt32(0);
        }

        public async Task<List<Subscription>> GetUserSubscriptionsAsync(ulong userId, ulong guildId)
        {
            return await Context.Subscriptions.AsNoTracking().Include(x => x.Item).AsAsyncEnumerable().Where(x => x.UserId == userId && x.GuildId == guildId).ToListAsync();
        }

        public async Task AddGuildIfNotExistsAsync(IGuild guild)
        {
            if (await Context.Guilds.AsNoTracking().AsAsyncEnumerable().AnyAsync(x => x.Id == guild.Id))
            {
                return;
            }

            await AddUserIfNotExistsAsync(await guild.GetOwnerAsync());

            var dbGuild = new Guild
            {
                Id = guild.Id,
                Name = guild.Name,
                Icon = guild.IconId,
                GuildConfig = new GuildConfig()
            };

            await Context.Guilds.AddAsync(dbGuild);
        }

        public async Task<GuildConfig> GetGuildConfigAsync(ulong guildId)
        {
            return await Context.GuildConfigs.AsNoTracking().AsAsyncEnumerable().SingleOrDefaultAsync(x => x.GuildId == guildId);
        }

        public async Task AddChannelIfNotExistsAsync(ITextChannel channel)
        {
            await AddChannelIfNotExistsAsync(channel, await GetGuildConfigAsync(channel.GuildId));
        }

        private async Task AddChannelIfNotExistsAsync(IChannel channel, GuildConfig guildConfig)
        {
            if (await Context.Channels.AsNoTracking().AsAsyncEnumerable().AnyAsync(x => x.Id == channel.Id))
            {
                return;
            }

            var dbChannel = new Channel
            {
                Id = channel.Id,
                GuildId = guildConfig.GuildId,
                Name = channel.Name,
                ChannelConfig = new ChannelConfig
                {
                    Logging = guildConfig.Logging,
                    CurrencyGen = guildConfig.Currency,
                    Xp = guildConfig.Xp
                }
            };

            await Context.Channels.AddAsync(dbChannel);
        }

        public async Task<long> GetUserCurrencyAsync(ulong userId, ulong guildId)
        {
            return await Context.UserData.AsNoTracking().AsAsyncEnumerable().Where(x => x.UserId == userId && x.GuildId == guildId).Select(x => x.Currency).SingleAsync();
        }

        public async Task<decimal> GetUserInvestingAsync(ulong userId, ulong guildId)
        {
            return await Context.UserData.AsNoTracking().AsAsyncEnumerable().Where(x => x.UserId == userId && x.GuildId == guildId).Select(x => x.Investing).SingleAsync();
        }

        public async Task<bool> RemoveCurrencyAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            UserData data = await Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == userId && x.GuildId == guildId);
            if (data.Currency - amount < 0)
            {
                return false;
            }
            data.Currency -= amount;

            UserData botData = await Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == Roki.BotId && x.GuildId == guildId);
            botData.Currency += amount;

            await Context.Transactions.AddAsync(new Transaction
            {
                GuildId = guildId,
                Sender = userId,
                Recipient = Roki.BotId,
                Amount = amount,
                ChannelId = channelId,
                MessageId = messageId,
                Description = reason
            });

            return true;
        }

        public async Task<bool> RemoveCurrencyAsync(UserData data, UserData botData, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            if (data.Currency - amount < 0)
            {
                return false;
            }
            data.Currency -= amount;
            botData.Currency += amount;
            
            await Context.Transactions.AddAsync(new Transaction
            {
                GuildId = guildId,
                Sender = data.UserId,
                Recipient = Roki.BotId,
                Amount = amount,
                ChannelId = channelId,
                MessageId = messageId,
                Description = reason
            });

            return true;
        }

        public async Task AddCurrencyAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            UserData data = await Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == userId && x.GuildId == guildId);
            data.Currency += amount;
            
            UserData botData = await Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == Roki.BotId && x.GuildId == guildId);
            botData.Currency -= amount;

            await Context.Transactions.AddAsync(new Transaction
            {
                GuildId = guildId,
                Sender = Roki.BotId,
                Recipient = userId,
                Amount = amount,
                ChannelId = channelId,
                MessageId = messageId,
                Description = reason
            });
        }

        public async Task AddCurrencyAsync(UserData data, UserData botData, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            data.Currency += amount;
            botData.Currency -= amount;

            await Context.Transactions.AddAsync(new Transaction
            {
                GuildId = guildId,
                Sender = Roki.BotId,
                Recipient = data.UserId,
                Amount = amount,
                ChannelId = channelId,
                MessageId = messageId,
                Description = reason
            });
        }

        public async Task<bool> TransferCurrencyAsync(ulong senderId, ulong recipientId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            UserData sender = await Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == senderId && x.GuildId == guildId);
            if (sender.Currency - amount < 0)
            {
                return false;
            }
            sender.Currency -= amount;

            UserData recipient = await Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == recipientId && x.GuildId == guildId);
            recipient.Currency += amount;

            await Context.Transactions.AddAsync(new Transaction
            {
                GuildId = guildId,
                Sender = senderId,
                Recipient = recipientId,
                Amount = amount,
                ChannelId = channelId,
                MessageId = messageId,
                Description = reason
            });

            return true;
        }

        public async Task<List<XpReward>> GetXpRewardsAsync(ulong guildId, int level)
        {
            return await Context.XpRewards.AsNoTracking().AsAsyncEnumerable().Where(x => x.GuildId == guildId && x.Level == level).ToListAsync();
        }
    }
}