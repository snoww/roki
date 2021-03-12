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
using Roki.Extensions;
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
        Task AddInvestingAsync(ulong userId, ulong guildId, decimal amount);
        Task<bool> RemoveInvestingAsync(ulong userId, ulong guildId, decimal amount);
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
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();

                var accounts = await context.UserData.AsNoTracking().AsQueryable()
                    .Where(x => x.UserId == userId && x.GuildId == guildId)
                    .Select(x => new {x.Currency})
                    .SingleOrDefaultAsync();

                if (accounts == null)
                {
                    UserData data;

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
                            return 0;
                        }

                        data = new UserData(userId, guildId)
                        {
                            User = new User(user.Username, user.Discriminator, user.AvatarId)
                        };
                    }

                    await context.UserData.AddAsync(data);
                    await context.SaveChangesAsync();
                    await _cache.StringSetAsync($"$c:{userId}:{guildId}", data.Currency, TimeSpan.FromDays(1));
                    return data.Currency;
                }

                await _cache.StringSetAsync($"$c:{userId}:{guildId}", accounts.Currency, TimeSpan.FromDays(1));
                return accounts.Currency;
            }

            await _cache.KeyExpireAsync($"$c:{userId}:{guildId}", TimeSpan.FromDays(1));
            return (long) cur;
        }

        public async Task<decimal> GetInvestingAsync(ulong userId, ulong guildId)
        {
            RedisValue cur = await _cache.StringGetAsync($"$i:{userId}:{guildId}");
            if (cur.IsNull)
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();

                var accounts = await context.UserData.AsNoTracking().AsQueryable()
                    .Where(x => x.UserId == userId && x.GuildId == guildId)
                    .Select(x => new {x.Investing})
                    .SingleOrDefaultAsync();

                if (accounts == null)
                {
                    UserData data;

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
                            return 0;
                        }

                        data = new UserData(userId, guildId)
                        {
                            User = new User(user.Username, user.Discriminator, user.AvatarId)
                        };
                    }

                    await context.UserData.AddAsync(data);
                    await context.SaveChangesAsync();
                    await _cache.StringSetAsync($"$i:{userId}:{guildId}", data.Investing.ToDouble(), TimeSpan.FromDays(1));
                    return data.Investing;
                }

                await _cache.StringSetAsync($"$i:{userId}:{guildId}", accounts.Investing.ToDouble(), TimeSpan.FromDays(1));
                return accounts.Investing;
            }

            await _cache.KeyExpireAsync($"$i:{userId}:{guildId}", TimeSpan.FromDays(1));
            return (decimal) cur;
        }

        private async Task SetBotAccountsCache(ulong guildId)
        {
            RedisKey[] keys = {new($"$i:{Roki.BotId}:{guildId}"), new($"$i:{Roki.BotId}:{guildId}")};
            if (await _cache.KeyExistsAsync(keys) == 2)
            {
                return;
            }

            using IServiceScope scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
            var accounts = await context.UserData.AsQueryable().Where(x => x.UserId == Roki.BotId && x.GuildId == guildId).Select(x => new {x.Currency, x.Investing}).SingleAsync();

            _cache.StringSet($"$c:{Roki.BotId}:{guildId}", accounts.Currency, TimeSpan.FromDays(1));
            _cache.StringSet($"$i:{Roki.BotId}:{guildId}", accounts.Investing.ToDouble(), TimeSpan.FromDays(1));
        }

        public async Task<bool> RemoveCurrencyAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            if (amount <= 0 || await GetCurrencyAsync(userId, guildId) < amount)
            {
                return false;
            }

            await SetBotAccountsCache(guildId);
            await _cache.StringIncrementAsync($"$c:{userId}:{guildId}", -amount);
            await _cache.StringIncrementAsync($"$c:{Roki.BotId}:{guildId}", amount);
            await DbAddRemoveCashAsync(userId, guildId, channelId, messageId, reason, -amount);
            return true;
        }

        public async Task AddCurrencyAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            // make sure it exists in cache
            await GetCurrencyAsync(userId, guildId);
            await SetBotAccountsCache(guildId);
            await _cache.StringIncrementAsync($"$c:{userId}:{guildId}", amount);
            await _cache.StringIncrementAsync($"$c:{Roki.BotId}:{guildId}", -amount);
            await DbAddRemoveCashAsync(userId, guildId, channelId, messageId, reason, amount);
        }

        public async Task<bool> TransferCurrencyAsync(ulong senderId, ulong recipientId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
        {
            if (amount <= 0 || await GetCurrencyAsync(senderId, guildId) < amount)
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
                if (amount <= 0 || await GetCurrencyAsync(userId, guildId) < amount)
                {
                    return false;
                }

                await _cache.StringIncrementAsync($"$c:{userId}:{guildId}", -amount);
                await _cache.StringIncrementAsync($"$i:{userId}:{guildId}", amount);
                await DbTransferAccountAsync(userId, guildId, channelId, messageId, amount, fromAccount, "Transfer from cash to investing account");
            }
            else
            {
                if (amount <= 0 || await GetInvestingAsync(userId, guildId) < amount)
                {
                    return false;
                }

                await _cache.StringIncrementAsync($"$c:{userId}:{guildId}", amount);
                await _cache.StringIncrementAsync($"$i:{userId}:{guildId}", -amount);
                await DbTransferAccountAsync(userId, guildId, channelId, messageId, amount, fromAccount, "Transfer from investing to cash account");
            }

            return true;
        }

        public async Task AddInvestingAsync(ulong userId, ulong guildId, decimal amount)
        {
            await SetBotAccountsCache(guildId);
            await _cache.StringIncrementAsync($"$i:{userId}:{guildId}", amount.ToDouble());
            await _cache.StringIncrementAsync($"$i:{Roki.BotId}:{guildId}", -amount.ToDouble());

            await DbAddRemoveInvestingAsync(userId, guildId, amount);
        }

        public async Task<bool> RemoveInvestingAsync(ulong userId, ulong guildId, decimal amount)
        {
            if (await GetInvestingAsync(userId, guildId) < amount)
            {
                return false;
            }

            await SetBotAccountsCache(guildId);
            await _cache.StringIncrementAsync($"$i:{userId}:{guildId}", -amount.ToDouble());
            await _cache.StringIncrementAsync($"$i:{Roki.BotId}:{guildId}", amount.ToDouble());
            await DbAddRemoveInvestingAsync(userId, guildId, -amount);
            return true;
        }

        // if positive amount: add to user's currency
        // if negative amount: remove user's currency
        private Task DbAddRemoveCashAsync(ulong userId, ulong guildId, ulong channelId, ulong messageId, string reason, long amount)
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

        private Task DbAddRemoveInvestingAsync(ulong userId, ulong guildId, decimal amount)
        {
            Task _ = Task.Run(async () =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
                List<UserData> data = await context.UserData.AsQueryable().Where(x => (x.UserId == userId || x.UserId == Roki.BotId) && x.GuildId == guildId).ToListAsync();
                if (data.Count != 2)
                {
                    Log.Error("Something went wrong when trying to add/remove currency from user: {userid}", userId);
                }

                if (data[0].UserId == Roki.BotId)
                {
                    data[0].Investing -= amount;
                    data[1].Investing += amount;
                }
                else
                {
                    data[0].Investing += amount;
                    data[1].Investing -= amount;
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