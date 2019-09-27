using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;

namespace Roki.Modules.Currency.Services
{
    public class PickDropService : IRService
    {
        private readonly CommandHandler _cmdHandler;
        private readonly DbService _db;

        public ConcurrentDictionary<ulong, DateTime> LastGenerations { get; } = new ConcurrentDictionary<ulong, DateTime>();
        private readonly SemaphoreSlim _pickLock = new SemaphoreSlim(1, 1);


        public PickDropService(CommandHandler cmdHandler, DbService db)
        {
            _cmdHandler = cmdHandler;
            _db = db;
            _cmdHandler.OnMessageNoTrigger += CurrencyGeneration;
        }
        
        private async Task CurrencyGeneration(IUserMessage message)
        {
            if (!(message is SocketUserMessage msg))
                return;
            if (!(message.Channel is ITextChannel channel))
                return;
            // TODO add ignored channels, change constants to database values

            var lastGeneration = LastGenerations.GetOrAdd(channel.Id, DateTime.MinValue);
            var rng = new Random();
            
            if (DateTime.UtcNow - TimeSpan.FromMinutes(5) < lastGeneration)
                return;

            var num = rng.Next(0, 100) + 15;
            if (num > 100 && LastGenerations.TryUpdate(channel.Id, DateTime.UtcNow, lastGeneration))
            {
                var drop = 1;
                var dropMax = 5;
                
                if (dropMax != null && dropMax > drop)
                    drop = new Random().Next(drop, dropMax + 1);

                if (drop > 0)
                {
                    var prefix = _cmdHandler.DefaultPrefix;
                    var toSend = drop == 1
                        ? $"<:stone:269130892100763649> A random stone appeared! Type `{prefix}pick` to pick it up."
                        : $"<:stone:269130892100763649> {drop} random stones appeared! Type `{prefix}pick` to pick them up.";
                    // TODO add images to send with drop
                    var curMessage = await channel.SendMessageAsync(toSend).ConfigureAwait(false);
                    using (var uow = _db.GetDbContext())
                    {
                        uow.Transaction.Add(new CurrencyTransaction
                        {
                            Amount = drop,
                            Reason = "GCA",
                            From = "Server",
                            To = "-",
                            GuildId = channel.GuildId,
                            ChannelId = channel.Id,
                            MessageId = curMessage.Id
                        });
                        await uow.SaveChangesAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        public async Task<long> PickAsync(ulong guildId, ITextChannel channel, IUser user)
        {
            await _pickLock.WaitAsync();
            try
            {
                long amount;
                ulong[] ids;
                using (var uow = _db.GetDbContext())
                {
                    (amount, ids) = await uow.Transaction.GetAndUpdateGeneratedCurrency(channel.Id, user.Id).ConfigureAwait(false);

                    if (amount > 0)
                    {
                        await uow.DUsers.UpdateCurrencyAsync(user, amount).ConfigureAwait(false);
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
            using (var uow = _db.GetDbContext())
            {
                var dUser = uow.DUsers.GetOrCreate(user);
                if (dUser.Currency < amount)
                    return false;

                var updated = await uow.DUsers.UpdateCurrencyAsync(user, -amount).ConfigureAwait(false);

                if (!updated) return false;
                
                var msg = await ctx.Channel.SendMessageAsync($"{user.Username} dropped {amount} <:stone:269130892100763649>\nType `.pick` to pick it up.");

                uow.Transaction.Add(new CurrencyTransaction
                {
                    Amount = amount,
                    Reason = "UserDrop",
                    From = dUser.UserId.ToString(),
                    To = "-",
                    GuildId = ctx.Guild.Id,
                    ChannelId = msg.Channel.Id,
                    MessageId = msg.Id
                });
                
                await uow.SaveChangesAsync().ConfigureAwait(false);
                return true;
            }
        }
    }
}