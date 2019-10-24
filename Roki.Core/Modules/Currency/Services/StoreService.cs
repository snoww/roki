using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Services;

namespace Roki.Modules.Currency.Services
{
    public class StoreService : IRService
    {
        private readonly DiscordSocketClient _client;
        private readonly ICurrencyService _currency;
        private readonly DbService _db;

        public StoreService(DiscordSocketClient client, ICurrencyService currency, DbService db)
        {
            _client = client;
            _currency = currency;
            _db = db;
        }

        public List<Listing> GetStoreCatalog()
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.Listing.GetStoreCatalog();
            }
        }

        public Listing GetListingByName(string name)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.Listing.GetListingByName(name);
            }
        }
        
        public Listing GetListingById(int id)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.Listing.GetListingById(id);
            }
        }

        public async Task UpdateListingAsync(int id)
        {
            using (var uow = _db.GetDbContext())
            {
                await uow.Listing.UpdateQuantityAsync(id).ConfigureAwait(false);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}