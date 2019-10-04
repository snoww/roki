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
        Task<bool> ChangeAsync(IUser user, string reason, long amount, string from, string to, ulong guildId, ulong channelId, ulong messageId);
        Task<bool> TransferAsync(IUser userFrom, IUser userTo, string reason, long amount, ulong guildId, ulong channelId, ulong messageId);
        Task ChangeListAsync(IEnumerable<IUser> users, IEnumerable<string> reasons, IEnumerable<long> amounts, IEnumerable<string> from, 
            IEnumerable<string> to, IEnumerable<ulong> guildIds, IEnumerable<ulong> channelIds, IEnumerable<ulong> messageIds);
    }
    
    public class CurrencyService : ICurrencyService
    {
        private readonly DbService _db;

        public CurrencyService(DbService db)
        {
            _db = db;
        }
        
        private CurrencyTransaction CreateTransaction(string reason, long amount, string from, string to, ulong guildId, ulong channelId, ulong messageId) =>
            new CurrencyTransaction
            {
                Amount = amount,
                Reason = reason ?? "-",
                To = to ?? "-",
                From = from ?? "-",
                GuildId = guildId,
                ChannelId = channelId,
                MessageId = messageId
            };

        private async Task<bool> InternalChangeAsync(IUser user, string reason, long amount, string from, string to, ulong guildId, ulong channelId, 
            ulong messageId)
        {
            using (var uow = _db.GetDbContext())
            {
                var success = await uow.DUsers.UpdateCurrencyAsync(user, amount).ConfigureAwait(false);
                if (success)
                {
                    var _ = CreateTransaction(reason, amount, from, to, guildId, channelId, messageId);
                    uow.Transaction.Add(_);
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
                return success;
            }
        }

        private async Task<bool> InternalTransferAsync(IUser userFrom, IUser userTo, string reason, long amount, ulong guildId, ulong channelId, 
            ulong messageId)
        {
            using (var uow = _db.GetDbContext())
            {
                var success = await uow.DUsers.UpdateCurrencyAsync(userFrom, amount).ConfigureAwait(false);
                if (success)
                {
                    await uow.DUsers.UpdateCurrencyAsync(userTo, amount).ConfigureAwait(false);
                    var _ = CreateTransaction(reason, amount, userFrom.Id.ToString(), userTo.Id.ToString(), guildId, channelId, messageId);
                    uow.Transaction.Add(_);
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
                return success;
            }
        }

        public async Task<bool> ChangeAsync(IUser user, string reason, long amount, string from, string to, ulong guildId, ulong channelId, ulong messageId)
        {
            return await InternalChangeAsync(user, reason, amount, from, to, guildId, channelId, messageId).ConfigureAwait(false);
        }

        public async Task<bool> TransferAsync(IUser userFrom, IUser userTo, string reason, long amount, ulong guildId, ulong channelId, ulong messageId)
        {
            return await InternalTransferAsync(userFrom, userTo, reason, amount, guildId, channelId, messageId);
        }

        public async Task ChangeListAsync(IEnumerable<IUser> users, IEnumerable<string> reasons, IEnumerable<long> amounts, IEnumerable<string> from,
            IEnumerable<string> to, IEnumerable<ulong> guildIds, IEnumerable<ulong> channelIds, IEnumerable<ulong> messageIds)
        {
            var usersArr = users as IUser[] ?? users.ToArray();
            var reasonsArr = reasons as string[] ?? reasons.ToArray();
            var amountsArr = amounts as long[] ?? amounts.ToArray();
            var fromArr = from as string[] ?? from.ToArray();
            var toArr = to as string[] ?? to.ToArray();
            var guildsArr = guildIds as ulong[] ?? guildIds.ToArray();
            var chansArr = channelIds as ulong[] ?? channelIds.ToArray();
            var msgsArr = messageIds as ulong[] ?? messageIds.ToArray();
            
            if (usersArr.Length != amountsArr.Length)
                throw new ArgumentException("Cannot perform bulk operation. Arrays are not of equal length");

            using (var uow = _db.GetDbContext())
            {
                for (int i = 0; i < usersArr.Length; i++)
                {
                    await InternalChangeAsync(usersArr[i], reasonsArr[i], amountsArr[i], fromArr[i], toArr[i], guildsArr[i], chansArr[i],
                        msgsArr[i]).ConfigureAwait(false);
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
            }

        }
    }
}