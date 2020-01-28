using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Services.Database.Core;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IListingRepository : IRepository<Listing>
    {
        List<Listing> GetStoreCatalog();
        Listing GetListingByName(string name);
        Listing GetListingById(int id);
        Task UpdateQuantityAsync(int id, int amount);
    }

    public class ListingRepository : Repository<Listing>, IListingRepository
    {
        public ListingRepository(DbContext context) : base(context)
        {
        }

        public List<Listing> GetStoreCatalog()
        {
            return Set.OrderByDescending(l => l.Cost).ToList();
        }

        public Listing GetListingByName(string name)
        {
            return Set.FirstOrDefault(l => l.Item == name);
        }

        public Listing GetListingById(int id)
        {
            return Set.FirstOrDefault(l => l.Id == id);
        }

        public async Task UpdateQuantityAsync(int id, int amount)
        {
            var listing = await Set.FirstAsync(l => l.Id == id).ConfigureAwait(false);
            listing.Quantity -= amount;
            await Context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}