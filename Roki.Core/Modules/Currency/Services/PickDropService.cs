using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Core;
using StackExchange.Redis;

namespace Roki.Modules.Currency.Services
{
    public class PickDropService : IRokiService
    {
        private readonly CommandHandler _cmdHandler;
        private readonly DbService _db;
        private readonly IDatabase _cache;

        private ConcurrentDictionary<ulong, DateTime> LastGenerations { get; } = new ConcurrentDictionary<ulong, DateTime>();
        private readonly SemaphoreSlim _pickLock = new SemaphoreSlim(1, 1);


        public PickDropService(CommandHandler cmdHandler, DbService db, IRedisCache cache)
        {
            _cmdHandler = cmdHandler;
            _db = db;
            _cache = cache.Redis.GetDatabase();
            _cmdHandler.OnMessageNoTrigger += CurrencyGeneration;
        }
        
        private async Task CurrencyGeneration(IUserMessage message)
        {
            if (!(message is SocketUserMessage))
                return;
            if (!(message.Channel is ITextChannel channel))
                return;
            if (Roki.Properties.CurrencyGenIgnoredChannels.Contains(channel.Id))
                return;

            var lastGeneration = LastGenerations.GetOrAdd(channel.Id, DateTime.MinValue);
            var rng = new Random();
            
            if (DateTime.UtcNow - TimeSpan.FromMinutes(Roki.Properties.CurrencyGenerationCooldown) < lastGeneration)
                return;

            var num = rng.Next(0, 100) + Roki.Properties.CurrencyGenerationChance * 100;
            if (num > 100 && LastGenerations.TryUpdate(channel.Id, DateTime.UtcNow, lastGeneration))
            {
                var drop = Roki.Properties.CurrencyDropAmount;
                var dropMax = Roki.Properties.CurrencyDropAmountMax;
                
                if (dropMax != null && dropMax > drop)
                    drop = new Random().Next(drop, dropMax.Value + 1);
                if (new Random().Next(0, 101) == 100)
                    drop = Roki.Properties.CurrencyDropAmountRare ?? 100;
                
                if (drop > 0)
                {
                    var prefix = Roki.Properties.Prefix;
                    var toSend = drop == 1
                        ? $"{Roki.Properties.CurrencyIcon} A random {Roki.Properties.CurrencyName} appeared! Type `{prefix}pick` to pick it up."
                        : $"{Roki.Properties.CurrencyIcon} {drop} random {Roki.Properties.CurrencyNamePlural} appeared! Type `{prefix}pick` to pick them up.";
                    // TODO add images to send with drop
                    var curMessage = await channel.SendMessageAsync(toSend).ConfigureAwait(false);
                    using var uow = _db.GetDbContext();
                    uow.Transaction.Add(new CurrencyTransaction
                    {
                        Amount = drop,
                        Reason = "GCA",
                        From = Roki.Properties.BotId,
                        GuildId = channel.GuildId,
                        ChannelId = channel.Id,
                        MessageId = curMessage.Id
                    });
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task<long> PickAsync(ITextChannel channel, IUser user)
        {
            await _pickLock.WaitAsync();
            try
            {
                long amount;
                ulong[] ids;
                using (var uow = _db.GetDbContext())
                {
                    (amount, ids) = await uow.Transaction.PickCurrency(channel.Id, user.Id).ConfigureAwait(false);
                    if (amount > 0)
                    {
                        await _cache.StringIncrementAsync($"currency:{channel.Guild.Id}:{user.Id}", amount, CommandFlags.FireAndForget)
                            .ConfigureAwait(false);
                        await uow.Users.UpdateCurrencyAsync(user.Id, amount).ConfigureAwait(false);
                    }

                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }

                try
                {
                    if (ids[0] == 0)
                        return 0;
                    var _ = channel.DeleteMessagesAsync(ids);
                }
                catch
                {
                    //
                }

                return amount;
            }
            finally
            {
                _pickLock.Release();
            }
        }

        public async Task<bool> DropAsync(ICommandContext ctx, IUser user, long amount)
        {
            using var uow = _db.GetDbContext();
            var dUser = await uow.Users.GetUserAsync(user.Id).ConfigureAwait(false);
            if (dUser.Currency < amount)
                return false;

            await _cache.StringIncrementAsync($"currency:{ctx.Guild.Id}:{user.Id}", -amount, CommandFlags.FireAndForget)
                .ConfigureAwait(false);
            var updated = await uow.Users.UpdateCurrencyAsync(user.Id, -amount).ConfigureAwait(false);

            if (!updated) return false;
                
            var msg = await ctx.Channel.SendMessageAsync($"{user.Username} dropped {amount:N0} {Roki.Properties.CurrencyIcon}\nType `.pick` to pick it up.");

            uow.Transaction.Add(new CurrencyTransaction
            {
                Amount = amount,
                Reason = "UserDrop",
                From = dUser.UserId,
                GuildId = ctx.Guild.Id,
                ChannelId = msg.Channel.Id,
                MessageId = msg.Id
            });
                
            await uow.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }
    }
}