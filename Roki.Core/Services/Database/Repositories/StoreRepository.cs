using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IStoreRepository : IRepository<Store>
    {
        List<Store> GetStoreCatalog();
    }

    public class StoreRepository : Repository<Store>, IStoreRepository
    {
        public StoreRepository(DbContext context) : base(context)
        {
        }

        public List<Store> GetStoreCatalog()
        {
            return Set.OrderByDescending(c => c.Cost).AsEnumerable().ToList();
        }
    }
}