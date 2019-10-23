using System.Collections.Generic;
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

        public List<Store> GetStoreCatalog()
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.Store.GetStoreCatalog();
            }
        }
    }
}