using System;
using System.Threading.Tasks;
using Discord;
using Roki.Services.Database.Maps;
using StackExchange.Redis;

namespace Roki.Services
{
    public interface ICurrencyService : IRokiService
    {
        Task<bool> RemoveAsync(IUser user, IUser bot, string reason, long amount, ulong guildId, ulong channelId, ulong messageId);
        Task AddAsync(IUser user, IUser bot, string reason, long amount, ulong guildId, ulong channelId, ulong messageId);
        Task<bool> TransferAsync(IUser userFrom, IUser userTo, string reason, long amount, ulong guildId, ulong channelId, ulong messageId);
        Task<long> GetCurrency(IUser user, ulong guildId);
        Task<bool> CacheChangeAsync(IUser user, string guildId, long amount);
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

        public async Task<bool> RemoveAsync(IUser user, IUser bot, string reason, long amount, ulong guildId, ulong channelId, ulong messageId)
        {
            bool success = await CacheChangeAsync(user, guildId.ToString(), -amount).ConfigureAwait(false);
            if (!success)
            {
                return false;
            }

            await ChangeBotCacheAsync(guildId, amount).ConfigureAwait(false);
            await ChangeDbAsync(user, bot, reason, -amount, guildId.ToString(), channelId, messageId).ConfigureAwait(false);
            return true;
        }

        public async Task AddAsync(IUser user, IUser bot, string reason, long amount, ulong guildId, ulong channelId, ulong messageId)
        {
            await CacheChangeAsync(user, guildId.ToString(), amount).ConfigureAwait(false);
            await ChangeBotCacheAsync(guildId, -amount).ConfigureAwait(false);
            await ChangeDbAsync(bot, user, reason, amount, guildId.ToString(), channelId, messageId).ConfigureAwait(false);
        }

        public async Task<bool> TransferAsync(IUser userFrom, IUser userTo, string reason, long amount, ulong guildId, ulong channelId, ulong messageId)
        {
            return await InternalTransferAsync(userFrom, userTo, reason, amount, guildId.ToString(), channelId, messageId);
        }

        public async Task<long> GetCurrency(IUser user, ulong guildId)
        {
            RedisValue cached = await _cache.StringGetAsync($"currency:{guildId}:{user.Id}").ConfigureAwait(false);
            if (cached.HasValue)
            {
                return (long) cached;
            }
            
            long balance = await _mongo.Context.GetUserCurrency(user, guildId.ToString());
            await _cache.StringSetAsync($"currency:{guildId}:{user.Id}", balance, TimeSpan.FromDays(7), flags: CommandFlags.FireAndForget).ConfigureAwait(false);
            return balance;
        }

        public async Task<bool> CacheChangeAsync(IUser user, string guildId, long amount)
        {
            if (amount == 0)
            {
                return false;
            }

            RedisValue currency = await _cache.StringGetAsync($"currency:{guildId}:{user.Id}").ConfigureAwait(false);
            long balance;
            if (currency.HasValue)
            {
                balance = (long) currency;
            }
            else
            {
                balance = await _mongo.Context.GetUserCurrency(user, guildId);
                await _cache.StringSetAsync($"currency:{guildId}:{user.Id}", balance, TimeSpan.FromDays(7)).ConfigureAwait(false);
            }
            
            if (balance + amount < 0) return false;

            await _cache.StringIncrementAsync($"currency:{guildId}:{user.Id}", amount, CommandFlags.FireAndForget).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> InternalTransferAsync(IUser userFrom, IUser userTo, string reason, long amount, string guildId, ulong channelId,
            ulong messageId)
        {
            bool success = await _mongo.Context.UpdateUserCurrencyAsync(userFrom, guildId, -amount).ConfigureAwait(false);
            if (success)
            {
                await CacheChangeAsync(userFrom, guildId, -amount).ConfigureAwait(false);
                await CacheChangeAsync(userTo, guildId, amount).ConfigureAwait(false);
                await _mongo.Context.UpdateUserCurrencyAsync(userTo, guildId, amount).ConfigureAwait(false);

                await _mongo.Context.AddTransaction(new Transaction
                {
                    Amount = amount,
                    Reason = reason ?? "-",
                    To = userTo.Id,
                    From = userFrom.Id,
                    GuildId = ulong.Parse(guildId),
                    ChannelId = channelId,
                    MessageId = messageId
                });
            }

            return success;
        }

        private Task ChangeDbAsync(IUser from, IUser to, string reason, long amount, string guildId, ulong channelId, ulong messageId)
        {
            Task _ = Task.Run(async () =>
            {
                // todo bot id
                if (from.Id == Roki.Properties.BotId)
                {
                    await _mongo.Context.UpdateBotCurrencyAsync(-amount, guildId).ConfigureAwait(false);
                    await _mongo.Context.UpdateUserCurrencyAsync(to, guildId, amount).ConfigureAwait(false);
                }
                else if (to.Id == Roki.Properties.BotId)
                {
                    await _mongo.Context.UpdateBotCurrencyAsync(-amount, guildId).ConfigureAwait(false);
                    await _mongo.Context.UpdateUserCurrencyAsync(from, guildId, amount).ConfigureAwait(false);
                }
                else
                {
                    await _mongo.Context.UpdateUserCurrencyAsync(from, guildId, amount).ConfigureAwait(false);
                }

                await _mongo.Context.AddTransaction(new Transaction
                {
                    Amount = amount,
                    Reason = reason ?? "-",
                    To = to.Id,
                    From = from.Id,
                    GuildId = ulong.Parse(guildId),
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