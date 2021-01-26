using System;
using System.Threading.Tasks;
using Roki.Services.Database.Maps;
using StackExchange.Redis;

namespace Roki.Services
{
    public interface ICurrencyService : IRokiService
    {
        Task<bool> RemoveAsync(ulong userId, string reason, long amount, ulong guildId, ulong channelId, ulong messageId);
        Task AddAsync(ulong userId, string reason, long amount, ulong guildId, ulong channelId, ulong messageId);
        Task<bool> TransferAsync(ulong userIdFrom, ulong userIdTo, string reason, long amount, ulong guildId, ulong channelId, ulong messageId);
        Task<long> GetCurrency(ulong userId, ulong guildId);
        Task<bool> CacheChangeAsync(ulong userId, ulong guildId, long amount);
    }

    public class CurrencyService : ICurrencyService
    {
        private readonly IDatabase _cache;
        private readonly IMongoService _mongo;

        public CurrencyService(IMongoService mongo, IRedisCache cache)
        {
            _mongo = mongo;
            _cache = cache.Redis.GetDatabase();
        }

        public async Task<bool> RemoveAsync(ulong userId, string reason, long amount, ulong guildId, ulong channelId, ulong messageId)
        {
            bool success = await CacheChangeAsync(userId, guildId, -amount).ConfigureAwait(false);
            if (!success)
            {
                return false;
            }

            await ChangeBotCacheAsync(guildId, amount).ConfigureAwait(false);
            await ChangeDbAsync(userId, Roki.Properties.BotId, reason, -amount, guildId, channelId, messageId).ConfigureAwait(false);
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

        public async Task<long> GetCurrency(ulong userId, ulong guildId)
        {
            RedisValue cached = await _cache.StringGetAsync($"currency:{guildId}:{userId}").ConfigureAwait(false);
            if (cached.HasValue)
            {
                return (long) cached;
            }
            
            long balance = _mongo.Context.GetUserCurrency(userId);
            await _cache.StringSetAsync($"currency:{guildId}:{userId}", balance, TimeSpan.FromDays(7), flags: CommandFlags.FireAndForget).ConfigureAwait(false);
            return balance;
        }

        public async Task<bool> CacheChangeAsync(ulong userId, ulong guildId, long amount)
        {
            if (amount == 0)
            {
                return false;
            }

            RedisValue currency = await _cache.StringGetAsync($"currency:{guildId}:{userId}").ConfigureAwait(false);
            long balance;
            if (currency.HasValue)
            {
                balance = (long) currency;
            }
            else
            {
                balance = _mongo.Context.GetUserCurrency(userId);
                await _cache.StringSetAsync($"currency:{guildId}:{userId}", balance, TimeSpan.FromDays(7)).ConfigureAwait(false);
            }
            
            if (balance + amount < 0) return false;

            await _cache.StringIncrementAsync($"currency:{guildId}:{userId}", amount, CommandFlags.FireAndForget).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> InternalTransferAsync(ulong userIdFrom, ulong userIdTo, string reason, long amount, ulong guildId, ulong channelId,
            ulong messageId)
        {
            bool success = await _mongo.Context.UpdateUserCurrencyAsync(userIdFrom, -amount).ConfigureAwait(false);
            if (success)
            {
                await CacheChangeAsync(userIdFrom, guildId, -amount).ConfigureAwait(false);
                await CacheChangeAsync(userIdTo, guildId, amount).ConfigureAwait(false);
                await _mongo.Context.UpdateUserCurrencyAsync(userIdTo, amount).ConfigureAwait(false);

                await _mongo.Context.AddTransaction(new Transaction
                {
                    Amount = amount,
                    Reason = reason ?? "-",
                    To = userIdTo,
                    From = userIdFrom,
                    GuildId = guildId,
                    ChannelId = channelId,
                    MessageId = messageId
                });
            }

            return success;
        }

        private Task ChangeDbAsync(ulong from, ulong to, string reason, long amount, ulong guildId, ulong channelId, ulong messageId)
        {
            Task _ = Task.Run(async () =>
            {
                if (from == Roki.Properties.BotId)
                {
                    await _mongo.Context.UpdateBotCurrencyAsync(-amount).ConfigureAwait(false);
                    await _mongo.Context.UpdateUserCurrencyAsync(to, amount).ConfigureAwait(false);
                }
                else if (to == Roki.Properties.BotId)
                {
                    await _mongo.Context.UpdateBotCurrencyAsync(-amount).ConfigureAwait(false);
                    await _mongo.Context.UpdateUserCurrencyAsync(from, amount).ConfigureAwait(false);
                }
                else
                {
                    await _mongo.Context.UpdateUserCurrencyAsync(from, amount).ConfigureAwait(false);
                }

                await _mongo.Context.AddTransaction(new Transaction
                {
                    Amount = amount,
                    Reason = reason ?? "-",
                    To = to,
                    From = from,
                    GuildId = guildId,
                    ChannelId = channelId,
                    MessageId = messageId
                });
            });

            return Task.CompletedTask;
        }

        private async Task ChangeBotCacheAsync(ulong guildId, long amount)
        {
            await _cache.StringIncrementAsync($"currency:{guildId}:{Roki.Properties.BotId}", amount, CommandFlags.FireAndForget).ConfigureAwait(false);
        }
    }
}