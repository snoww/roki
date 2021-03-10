using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Modules.Currency;
using Roki.Services.Database;
using Roki.Services.Database.Models;
using StackExchange.Redis;

namespace Roki.Services
{
    public interface ICurrencyService
    {
        Task<long> GetCurrencyAsync(ulong userId, ulong guildId);
        Task<decimal> GetInvestingAsync(ulong userId, ulong guildId);
        Task<bool> RemoveCurrencyAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount);
        Task AddCurrencyAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount);
        Task<bool> TransferCurrencyAsync(ulong senderId, ulong recipientId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount);
        Task<bool> TransferAccountAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, long amount, int fromAccount);
    }
    
    public class CurrencyService : ICurrencyService
    {
        private readonly DiscordSocketClient _client;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDatabase _cache;
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public CurrencyService(DiscordSocketClient client, IServiceScopeFactory scopeFactory, IRedisCache cache)
        {
            _client = client;
            _scopeFactory = scopeFactory;
            _cache = cache.Redis.GetDatabase();
        }
        
        public async Task<long> GetCurrencyAsync(ulong userId, ulong guildId)
        {
            RedisValue cur = await _cache.StringGetAsync($"$c:{userId}:{guildId}");
            if (cur.IsNull)
            {
                UserData data = await GetOrCreateUserDataAsync(userId, guildId);
                if (data == null)
                {
                    return 0;
                }
                await _cache.StringSetAsync($"$c:{userId}:{guildId}", data.Currency, TimeSpan.FromDays(1));
                return data.Currency;
            }

            await _cache.KeyExpireAsync($"$c:{userId}:{guildId}", TimeSpan.FromDays(1));
            return (long) cur;
        }

        public async Task<decimal> GetInvestingAsync(ulong userId, ulong guildId)
        {
            RedisValue cur = await _cache.StringGetAsync($"$i:{userId}:{guildId}");
            if (cur.IsNull)
            {
                UserData data = await GetOrCreateUserDataAsync(userId, guildId);
                if (data == null)
                {
                    return 0;
                }
                
                await _cache.StringSetAsync($"$i:{userId}:{guildId}", data.Investing.ToString(CultureInfo.InvariantCulture));
                return data.Investing;
            }

            return decimal.Parse(cur, NumberStyles.AllowDecimalPoint);
        }

        public async Task<bool> RemoveCurrencyAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            long currency = await GetCurrencyAsync(userId, guildId);
            if (amount <= 0 || currency - amount < 0)
            {
                return false;
            }

            await _cache.StringIncrementAsync($"$c:{userId}:{guildId}", -amount);
            await _cache.StringIncrementAsync($"$c:{Roki.BotId}:{guildId}", amount);
            await DbAddRemoveAsync(userId, guildId, channelId, messageId, reason, -amount);
            return true;
        }

        public async Task AddCurrencyAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            // make sure it exists in cache
            await GetCurrencyAsync(userId, guildId);
            await _cache.StringIncrementAsync($"$c:{userId}:{guildId}", amount);
            await _cache.StringIncrementAsync($"$c:{Roki.BotId}:{guildId}", -amount);
            await DbAddRemoveAsync(userId, guildId, channelId, messageId, reason, amount);
        }

        public async Task<bool> TransferCurrencyAsync(ulong senderId, ulong recipientId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            long senderCurrency = await GetCurrencyAsync(senderId, guildId);
            if (amount <= 0 || senderCurrency - amount < 0)
            {
                return false;
            }
            // make sure recipient exists in cache
            await GetCurrencyAsync(recipientId, guildId);
            await _cache.StringIncrementAsync($"$c:{senderId}:{guildId}", -amount);
            await _cache.StringIncrementAsync($"$c:{recipientId}:{guildId}", amount);
            await DbTransferAsync(senderId, recipientId, guildId, channelId, messageId, reason, amount);
            return true;
        }

        public async Task<bool> TransferAccountAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, long amount, int fromAccount)
        {
            // 0 = currency
            // 1 = investing
            if (fromAccount == 0)
            {
                long currency = await GetCurrencyAsync(userId, guildId);
                if (amount <= 0 || currency - amount < 0)
                {
                    return false;
                }
                await _cache.StringIncrementAsync($"$c:{userId}:{guildId}", -amount);
                decimal investing = await GetInvestingAsync(userId, guildId);
                investing += amount;
                await _cache.StringSetAsync($"$i:{userId}:{guildId}", investing.ToString(CultureInfo.InvariantCulture));
                await DbTransferAccountAsync(userId, guildId, channelId, messageId, amount, fromAccount, "Transfer from cash to investing account");
            }
            else
            {
                decimal investing = await GetInvestingAsync(userId, guildId);
                if (amount <= 0 || investing - amount < 0)
                {
                    return false;
                }
                await _cache.StringIncrementAsync($"$c:{userId}:{guildId}", amount);
                investing -= amount;
                await _cache.StringSetAsync($"$i:{userId}:{guildId}", investing.ToString(CultureInfo.InvariantCulture));
                await DbTransferAccountAsync(userId, guildId, channelId, messageId, amount, fromAccount, "Transfer from investing to cash account");
            }

            return true;
        }

        private async Task<UserData> GetOrCreateUserDataAsync(ulong userId, ulong guildId)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RokiContext>();

            UserData data = await context.UserData.AsQueryable().Where(x => x.UserId == userId && x.GuildId == guildId).SingleOrDefaultAsync();
            if (data == null)
            {
                if (await context.Users.AsNoTracking().AnyAsync(x => x.Id == userId))
                {
                    data = new UserData(userId, guildId);
                }
                else
                {
                    IUser user = _client.GetUser(userId);
                    if (user == null)
                    {
                        Log.Warn("User {userid} not found.", userId);
                        return null;
                    }
                    data = new UserData(userId, guildId)
                    {
                        User = new User(user.Username, user.Discriminator, user.AvatarId)
                    };
                }

                await context.UserData.AddAsync(data);
                await context.SaveChangesAsync();
            }
            
            return data;
        }

        // if positive amount: add to user's currency
        // if negative amount: remove user's currency
        private Task DbAddRemoveAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                List<UserData> data = await context.UserData.AsQueryable().Where(x => (x.UserId == userId || x.UserId == Roki.BotId) && x.GuildId == guildId).Take(2).ToListAsync();
                if (data.Count != 2)
                {
                    Log.Error("Something went wrong when trying to add/remove currency from user: {userid}", userId);
                }

                if (data[0].UserId == Roki.BotId)
                {
                    data[0].Currency -= amount;
                    data[1].Currency += amount;
                }
                else
                {
                    data[0].Currency += amount;
                    data[1].Currency -= amount;
                }

                if (amount > 0)
                {
                    await context.Transactions.AddAsync(new Transaction(Roki.BotId, userId, guildId, channelId, messageId, amount, reason));
                }
                else
                {
                    await context.Transactions.AddAsync(new Transaction(userId, Roki.BotId, guildId, channelId, messageId, amount, reason));
                }
                
                await context.SaveChangesAsync();
            });
            
            return Task.CompletedTask;
        }

        private Task DbTransferAsync(ulong senderId, ulong recipientId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                List<UserData> data = await context.UserData.AsQueryable().Where(x => (x.UserId == senderId || x.UserId == recipientId) && x.GuildId == guildId).Take(2).ToListAsync();
                if (data.Count != 2)
                {
                    Log.Error("Something went wrong when trying to transfer currency from user: {senderid} to: {recipientid}", senderId, recipientId);
                }
                if (data[0].UserId == senderId)
                {
                    data[0].Currency -= amount;
                    data[1].Currency += amount;
                }
                else
                {
                    data[0].Currency += amount;
                    data[1].Currency -= amount;
                }

                await context.Transactions.AddAsync(new Transaction(senderId, recipientId, guildId, channelId, messageId, amount, reason));
                await context.SaveChangesAsync();
            });
            
            return Task.CompletedTask;
        }

        private Task DbTransferAccountAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, long amount, int fromAccount, string description)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                UserData data = await context.UserData.AsQueryable().SingleAsync(x => x.UserId == userId && x.GuildId == guildId);
                if (fromAccount == 0)
                {
                    data.Currency -= amount;
                    data.Investing += amount;
                }
                else
                {
                    data.Currency += amount;
                    data.Investing -= amount;
                }
                
                await context.Transactions.AddAsync(new Transaction(userId, userId, guildId, channelId, messageId, amount, description));

                await context.SaveChangesAsync();
            });
            
            return Task.CompletedTask;
        }
    }
}