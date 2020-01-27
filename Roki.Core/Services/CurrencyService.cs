using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using StackExchange.Redis;

namespace Roki.Services
{
    public interface ICurrencyService : IRokiService
    {
        Task<bool> RemoveAsync(ulong userId, string reason, long amount, ulong guildId, ulong channelId, ulong messageId);
        Task AddAsync(ulong userId, string reason, long amount, ulong guildId, ulong channelId, ulong messageId);
        Task<bool> TransferAsync(ulong userIdFrom, ulong userIdTo, string reason, long amount, ulong guildId, ulong channelId, ulong messageId);
        Task<long> GetCurrency(ulong userId, ulong guildId);
    }
    
    public class CurrencyService : ICurrencyService
    {
        private readonly DbService _db;
        private readonly IDatabase _cache;

        public CurrencyService(DbService db, IRedisCache cache)
        {
            _db = db;
            _cache = cache.Redis.GetDatabase();
        }

        public async Task<bool> RemoveAsync(ulong userId, string reason, long amount, ulong guildId, ulong channelId, ulong messageId)
        {
            var success = await CacheChangeAsync(userId, guildId, -amount).ConfigureAwait(false);
            if (!success)
                return false;

            await ChangeBotCacheAsync(guildId, amount).ConfigureAwait(false);
            await ChangeDbAsync(userId,Roki.Properties.BotId, reason, -amount, guildId, channelId, messageId).ConfigureAwait(false);
            return true;
        }

        public async Task AddAsync(ulong userId, string reason, long amount, ulong guildId, ulong channelId, ulong messageId)
        {
            await CacheChangeAsync(userId, guildId, amount).ConfigureAwait(false);
            await ChangeBotCacheAsync(guildId, -amount).ConfigureAwait(false);
            await ChangeDbAsync(Roki.Properties.BotId, userId, reason, amount, guildId, channelId, messageId).ConfigureAwait(false);
        }

        public async Task<bool> TransferAsync(ulong userIdFrom, ulong userIdTo, string reason, long amount, ulong guildId, ulong channelId, ulong messageId)
        {
            return await InternalTransferAsync(userIdFrom, userIdTo, reason, amount, guildId, channelId, messageId);
        }
        
        private static CurrencyTransaction CreateTransaction(string reason, long amount, ulong from, ulong to, ulong guildId, ulong channelId, ulong messageId) =>
            new CurrencyTransaction
            {
                Amount = amount,
                Reason = reason ?? "-",
                To = to,
                From = from,
                GuildId = guildId,
                ChannelId = channelId,
                MessageId = messageId
            };

        private async Task<bool> InternalTransferAsync(ulong userIdFrom, ulong userIdTo, string reason, long amount, ulong guildId, ulong channelId, 
            ulong messageId)
        {
            using var uow = _db.GetDbContext();
            var success = await uow.Users.UpdateCurrencyAsync(userIdFrom, -amount).ConfigureAwait(false);
            if (success)
            {
                await uow.Users.UpdateCurrencyAsync(userIdTo, amount).ConfigureAwait(false);
                var _ = CreateTransaction(reason, amount, userIdFrom, userIdTo, guildId, channelId, messageId);
                uow.Transaction.Add(_);
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
            return success;
        }

        private Task ChangeDbAsync(ulong from, ulong to, string reason, long amount, ulong guildId, ulong channelId, ulong messageId)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                if (from == Roki.Properties.BotId)
                {
                    await uow.Users.UpdateBotCurrencyAsync(from, -amount);
                    await uow.Users.UpdateCurrencyAsync(to, amount).ConfigureAwait(false);
                }
                else if (to == Roki.Properties.BotId)
                {
                    await uow.Users.UpdateBotCurrencyAsync(to, -amount);
                    await uow.Users.UpdateCurrencyAsync(from, amount).ConfigureAwait(false);
                }
                else
                {
                    await uow.Users.UpdateCurrencyAsync(from, amount).ConfigureAwait(false);
                }

                var transaction = CreateTransaction(reason, amount, from, to, guildId, channelId, messageId);
                uow.Transaction.Add(transaction);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });

            return Task.CompletedTask;
        }

        public async Task<long> GetCurrency(ulong userId, ulong guildId)
        {
            var currency = await _cache.StringGetAsync($"currency:{guildId}:{userId}").ConfigureAwait(false);
            if (currency.HasValue)
            {
                return (long) currency;
            }

            return GetDbCurrency(userId);
        }
        
        private long GetDbCurrency(ulong userId)
        {
            using var uow = _db.GetDbContext();
            return uow.Users.GetUserCurrency(userId);
        }

        private async Task<bool> CacheChangeAsync(ulong userId, ulong guildId, long amount)
        {
            if (amount == 0)
                return false;
            
            var currency = await _cache.StringGetAsync($"currency:{guildId}:{userId}").ConfigureAwait(false);
            if (currency.HasValue)
            {
                var cached = (long) currency;
                if (cached + amount < 0) return false;
                
                await _cache.StringIncrementAsync($"currency:{guildId}:{userId}", amount, CommandFlags.FireAndForget).ConfigureAwait(false);
                return true;
            }

            var currentCurrency = GetDbCurrency(userId);

            if (currentCurrency + amount >= 0)
            {
                await _cache.StringSetAsync($"currency:{guildId}:{userId}", currentCurrency + amount, flags: CommandFlags.FireAndForget).ConfigureAwait(false);
                return true;
            }
            
            await _cache.StringSetAsync($"currency:{guildId}:{userId}", currentCurrency, flags: CommandFlags.FireAndForget).ConfigureAwait(false);
            return false;
        }

        private async Task ChangeBotCacheAsync(ulong guildId, long amount)
        {
            var currency = await _cache.StringGetAsync($"currency:{guildId}:{Roki.Properties.BotId}").ConfigureAwait(false);
            if (currency.HasValue)
            {
                await _cache.StringIncrementAsync($"currency:{guildId}:{Roki.Properties.BotId}", amount, CommandFlags.FireAndForget).ConfigureAwait(false);
                return;
            }
            
            var currentCurrency = GetDbCurrency(Roki.Properties.BotId);
            await _cache.StringSetAsync($"currency:{guildId}:{Roki.Properties.BotId}", currentCurrency + amount, flags: CommandFlags.FireAndForget).ConfigureAwait(false);
        }
    }
}