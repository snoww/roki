using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Services;

namespace Roki.Modules.Currency.Services
{
    public class StoreService : IRService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;
        private Timer _timer;

        public StoreService(DiscordSocketClient client, DbService db)
        {
            _client = client;
            _db = db;
            CheckSubscriptions();
        }

        private void CheckSubscriptions()
        {
            var today = DateTime.UtcNow;
            var drawToday = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0);
            var drawTmr = new DateTime(today.Year, today.Month, today.Day + 1, 0, 0, 0);
            _timer = drawToday - today > TimeSpan.Zero 
                ? new Timer(RemoveSubEvent, null, drawToday - today, TimeSpan.FromDays(1)) 
                : new Timer(RemoveSubEvent, null, drawTmr - today, TimeSpan.FromDays(1));
        }

        private async void RemoveSubEvent(object state)
        {
            using (var uow = _db.GetDbContext())
            {
                var expired = uow.Subscriptions.GetExpiredSubscriptions();
                foreach (var sub in expired)
                {
                    await uow.Subscriptions.RemoveSubscriptionAsync(sub.Id).ConfigureAwait(false);
                    if (!(_client.GetUser(sub.UserId) is IGuildUser user)) continue;
                    var role = user.Guild.Roles.First(r => r.Name == sub.Description);
                    await user.RemoveRoleAsync(role).ConfigureAwait(false);
                }
            }
        }

        public async Task NewStoreItem(ulong sellerId, string itemName, string itemDetails, string itemDescription, string category,
            string type, TimeSpan? subTime, long cost, int quantity)
        {
            using (var uow = _db.GetDbContext())
            {
                uow.Listing.Add(new Listing
                {
                    SellerId = sellerId,
                    ItemName = itemName,
                    ItemDetails = itemDetails,
                    Description = itemDescription,
                    Category = category,
                    Cost = cost,
                    Type = type,
                    SubscriptionTime = subTime,
                    Quantity = quantity,
                    ListDate = DateTime.UtcNow
                });
                
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
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

        public async Task AddNewSubscriptionAsync(ulong userId, string description, DateTime startDate, DateTime endDate)
        {
            using (var uow = _db.GetDbContext())
            {
                await uow.Subscriptions.NewSubscriptionAsync(userId, description, startDate, endDate).ConfigureAwait(false);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}