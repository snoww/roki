using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;

namespace Roki.Services
{
    public interface ICurrencyService : IRService
    {
        Task ChangeAsync(IUser user, string reason, long amount);
        Task ChangeListAsync(IEnumerable<ulong> userIds, IEnumerable<string> reasons, IEnumerable<long> amounts);
    }
    
    public class CurrencyService : ICurrencyService
    {
        private readonly DbService _db;

        public CurrencyService(DbService db)
        {
            _db = db;
        }
        
        private CurrencyTransaction CreateTransaction(ulong userId, string reason, long amount) =>
            new CurrencyTransaction
            {
                Amount = amount,
                Reason = reason ?? "-",
                UserIdFrom = userId
            };

        private async Task InternalChangeAsync(IUser user, string reason, long amount)
        {
            using (var uow = _db.GetDbContext())
            {
                var success = await uow.DUsers.UpdateCurrency(user, amount).ConfigureAwait(false);
                if (success)
                {
                    var _ = CreateTransaction(user.Id, reason, amount);
                    uow.Transaction.Add(_);
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task ChangeAsync(IUser user, string reason, long amount)
        {
            await InternalChangeAsync(user, reason, amount).ConfigureAwait(false);
        }

        public async Task ChangeListAsync(IEnumerable<ulong> userIds, IEnumerable<string> reasons, IEnumerable<long> amounts)
        {
            throw new System.NotImplementedException();
        }
    }
}