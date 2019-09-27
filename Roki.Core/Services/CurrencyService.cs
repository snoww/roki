using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;

namespace Roki.Services
{
    public interface ICurrencyService : IRService
    {
        Task ChangeAsync(IUser user, string reason, long amount, string from, ulong guildId, ulong channelId, ulong messageId);
        Task ChangeListAsync(IEnumerable<IUser> users, IEnumerable<string> reasons, IEnumerable<long> amounts, IEnumerable<string> from,
            IEnumerable<ulong> guildIds, IEnumerable<ulong> channelIds, IEnumerable<ulong> messageIds);
    }
    
    public class CurrencyService : ICurrencyService
    {
        private readonly DbService _db;

        public CurrencyService(DbService db)
        {
            _db = db;
        }
        
        private CurrencyTransaction CreateTransaction(ulong userId, string reason, long amount, string from, ulong guildId, ulong channelId, ulong messageId) =>
            new CurrencyTransaction
            {
                Amount = amount,
                Reason = reason ?? "-",
                To = userId.ToString(),
                From = from ?? "-",
                GuildId = guildId,
                ChannelId = channelId,
                MessageId = messageId
            };

        private async Task InternalChangeAsync(IUser user, string reason, long amount, string from, ulong guildId, ulong channelId, ulong messageId)
        {
            using (var uow = _db.GetDbContext())
            {
                var success = await uow.DUsers.UpdateCurrency(user, amount).ConfigureAwait(false);
                if (success)
                {
                    var _ = CreateTransaction(user.Id, reason, amount, from, guildId, channelId, messageId);
                    uow.Transaction.Add(_);
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task ChangeAsync(IUser user, string reason, long amount, string from, ulong guildId, ulong channelId, ulong messageId)
        {
            await InternalChangeAsync(user, reason, amount, from, guildId, channelId, messageId).ConfigureAwait(false);
        }

        public async Task ChangeListAsync(IEnumerable<IUser> users, IEnumerable<string> reasons, IEnumerable<long> amounts, IEnumerable<string> from,
            IEnumerable<ulong> guildIds, IEnumerable<ulong> channelIds, IEnumerable<ulong> messageIds)
        {
            var usersArr = users as IUser[] ?? users.ToArray();
            var reasonsArr = reasons as string[] ?? reasons.ToArray();
            var amountsArr = amounts as long[] ?? amounts.ToArray();
            var fromArr = from as string[] ?? from.ToArray();
            var guildsArr = guildIds as ulong[] ?? guildIds.ToArray();
            var chansArr = channelIds as ulong[] ?? channelIds.ToArray();
            var msgsArr = messageIds as ulong[] ?? messageIds.ToArray();
            
            if (usersArr.Length != amountsArr.Length)
                throw new ArgumentException("Cannot perform bulk operation. Arrays are not of equal length");

            using (var uow = _db.GetDbContext())
            {
                for (int i = 0; i < usersArr.Length; i++)
                {
                    await InternalChangeAsync(usersArr[i], reasonsArr[i], amountsArr[i], fromArr[i], guildsArr[i], chansArr[i],
                        msgsArr[i]).ConfigureAwait(false);
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
            }

        }
    }
}