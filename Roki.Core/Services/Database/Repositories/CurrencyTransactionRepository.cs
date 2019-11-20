using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface ICurrencyTransactionRepository : IRepository<CurrencyTransaction>
    {
        Task<(long, ulong[])> PickCurrency(ulong channelId, ulong userId);
        List<CurrencyTransaction> GetTransactions(ulong userId, int page);
    }

    public class CurrencyTransactionRepository : Repository<CurrencyTransaction>, ICurrencyTransactionRepository
    {
        public CurrencyTransactionRepository(DbContext context) : base(context)
        {
        }

        public async Task<(long, ulong[])> PickCurrency(ulong channelId, ulong userId)
        {
            var trans = Set.Where(t => t.ChannelId == channelId && (t.Reason == "GCA" || t.Reason == "UserDrop")).ToArray();
            if (!trans.Any())
                return (0, new ulong[0]);
            var toReturn = (trans.Sum(t => t.Amount), trans.Select(t => t.MessageId).ToArray());
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE transactions
SET `Reason`={"Picked"},
    `To`={userId}
WHERE `ChannelId`={channelId} AND (`Reason`={"GCA"} OR `Reason`={"UserDrop"})
").ConfigureAwait(false);
            return toReturn;
        }

        public List<CurrencyTransaction> GetTransactions(ulong userId, int page)
        {
            return Set.Where(t => t.From == userId.ToString() || t.To == userId.ToString())
                .OrderByDescending(t => t.TransactionDate)
                .Skip(15 * page)
                .Take(15)
                .ToList();
        }
    }
}