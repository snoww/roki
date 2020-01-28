using Microsoft.EntityFrameworkCore;
using Roki.Services.Database.Core;

namespace Roki.Services.Database.Repositories
{
    public interface ITradesRepository : IRepository<Trades>
    {
        
    }
    
    public class TradesRepository : Repository<Trades>, ITradesRepository
    {
        public TradesRepository(DbContext context) : base(context)
        {
        }
    }
}