using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface ICurrencyTransactionRepository : IRepository<CurrencyTransaction>
    {
        
    }

    public class CurrencyTransactionRepository : Repository<CurrencyTransaction>, ICurrencyTransactionRepository
    {
        public CurrencyTransactionRepository(DbContext context) : base(context)
        {
        }
    }
}