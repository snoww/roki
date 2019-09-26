using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface ICurrencyTransactionRepository : IRepository<CurrencyTransaction>
    {
        Task<(long, ulong[])> GetAndUpdateGeneratedCurrency(ulong channelId, ulong userId);
    }

    public class CurrencyTransactionRepository : Repository<CurrencyTransaction>, ICurrencyTransactionRepository
    {
        public CurrencyTransactionRepository(DbContext context) : base(context)
        {
        }

        public async Task<(long, ulong[])> GetAndUpdateGeneratedCurrency(ulong channelId, ulong userId)
        {
            var trans = Set.Where(t => t.ChannelId == channelId && t.Reason == "GCA").ToArray();
            if (!trans.Any())
                return (0, new ulong[channelId]);
            var toReturn = (trans.Sum(t => t.Amount), trans.Select(t => t.MessageId).ToArray());
            await Context.Database.ExecuteSqlCommandAsync($@"
UPDATE IGNORE currencytransactions
SET Reason={"GCPicked"}
    To={userId}
WHERE ChannelId={channelId} AND Reason={"GCA"}
").ConfigureAwait(false);
            return toReturn;
        }
    }
}