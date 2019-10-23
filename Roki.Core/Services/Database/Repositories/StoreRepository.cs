using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IStoreRepository : IRepository<Store>
    {
        
    }

    public class StoreRepository : Repository<Store>, IStoreRepository
    {
        public StoreRepository(DbContext context) : base(context)
        {
        }
    }
}