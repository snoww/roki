using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Currency.Services
{
    public class StoreService : IRokiService
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
            _timer = new Timer(RemoveSubEvent, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        }

        private async void RemoveSubEvent(object state)
        {
            using var uow = _db.GetDbContext();
            var expired = uow.Subscriptions.GetExpiredSubscriptions();
            foreach (var sub in expired)
            {
                await uow.Subscriptions.RemoveSubscriptionAsync(sub.Id).ConfigureAwait(false);
                if (sub.Type.Equals("BOOST", StringComparison.OrdinalIgnoreCase)) continue;
                var guild = _client.GetGuild(sub.GuildId);
                if (!(guild.GetUser(sub.UserId) is IGuildUser user)) continue;
                var role = user.GetRoles().FirstOrDefault(r => r.Name.Contains(sub.Description, StringComparison.OrdinalIgnoreCase));
                if (role == null) continue;
                await user.RemoveRoleAsync(role).ConfigureAwait(false);
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task NewStoreItem(ulong sellerId, string itemName, string itemDetails, string itemDescription, string category,
            string type, int? subDays, long cost, int quantity)
        {
            using var uow = _db.GetDbContext();
            uow.Listing.Add(new Listing
            {
                SellerId = sellerId,
                Item = itemName,
                Details = itemDetails,
                Description = itemDescription,
                Category = category,
                Cost = cost,
                Type = type,
                SubscriptionDays = subDays,
                Quantity = quantity,
                ListDate = DateTimeOffset.UtcNow
            });
                
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        public List<Listing> GetStoreCatalog()
        {
            using var uow = _db.GetDbContext();
            return uow.Context.Listings.OrderByDescending(l => l.Cost).ToList();
        }

        public Listing GetListingByName(string name)
        {
            using var uow = _db.GetDbContext();
            return uow.Listing.GetListingByName(name);
        }
        
        public Listing GetListingById(int id)
        {
            using var uow = _db.GetDbContext();
            return uow.Listing.GetListingById(id);
        }

        public async Task UpdateListingAsync(int id, int amount)
        {
            using var uow = _db.GetDbContext();
            await uow.Listing.UpdateQuantityAsync(id, amount).ConfigureAwait(false);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task AddNewSubscriptionAsync(ulong userId, ulong guildId, int itemId, string type, string description, DateTime startDate, DateTime endDate)
        {
            using var uow = _db.GetDbContext();
            await uow.Subscriptions.NewSubscriptionAsync(userId, guildId, itemId, type, description, startDate, endDate).ConfigureAwait(false);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<bool> GetOrUpdateSubAsync(ulong userId, int itemId, int days)
        {
            using var uow = _db.GetDbContext();
            var subId = await uow.Subscriptions.CheckSubscription(userId, itemId).ConfigureAwait(false);
            if (subId == 0) return false;
            await uow.Subscriptions.UpdateSubscriptionsAsync(subId, days).ConfigureAwait(false);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public List<Subscriptions> GetUserSubscriptions(ulong userId)
        {
            using var uow = _db.GetDbContext();
            return uow.Subscriptions.GetUserSubscriptions(userId);
        }

        public async Task<IRole> GetRoleAsync(ICommandContext ctx, string roleName)
        {
            if (!roleName.Contains("rainbow", StringComparison.OrdinalIgnoreCase))
            {
                return ctx.Guild.Roles.First(r => r.Name == roleName);
            }
            var rRoles = ctx.Guild.Roles.Where(r => r.Name.Contains("rainbow", StringComparison.OrdinalIgnoreCase)).ToList();
            var first = rRoles.First();
            var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);

            foreach (var user in users)
            {
                var rRole = user.GetRoles().FirstOrDefault(r => r.Name.Contains("rainbow", StringComparison.OrdinalIgnoreCase));
                if (rRole == null) continue;
                rRoles.Remove(rRole);
            }

            return rRoles.First() ?? first;
        }

        public async Task<List<Item>> GetOrCreateInventoryAsync(ulong userId)
        {
            using var uow = _db.GetDbContext();
            return await uow.Users.GetUserInventory(userId).ConfigureAwait(false);
        }

        public async Task UpdateInventoryAsync(ulong userId, string name, int quantity)
        {
            using var uow = _db.GetDbContext();
            await uow.Users.UpdateUserInventory(userId, name, quantity).ConfigureAwait(false);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}