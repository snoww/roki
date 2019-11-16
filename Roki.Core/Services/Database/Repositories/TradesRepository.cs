using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
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