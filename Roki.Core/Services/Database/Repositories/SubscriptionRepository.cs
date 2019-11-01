using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface ISubscriptionRepository : IRepository<Subscriptions>
    {
        Task NewSubscriptionAsync(ulong userId, int itemId, string type, string description, DateTime startDate, DateTime endDate);
        Task RemoveSubscriptionAsync(int id);
        IEnumerable<Subscriptions> GetExpiredSubscriptions();
        Task<int> CheckSubscription(ulong userId, int itemId);
        Task UpdateSubscriptionsAsync(int id, int days);
        List<Subscriptions> GetUserSubscriptions(ulong userId);
        bool DoubleXpIsActive(ulong userId);
        bool FastXpIsActive(ulong userId);
    }
    
    public class SubscriptionRepository : Repository<Subscriptions>, ISubscriptionRepository
    {
        public SubscriptionRepository(DbContext context) : base(context)
        {
        }


        public async Task NewSubscriptionAsync(ulong userId, int itemId, string type, string description, DateTime startDate, DateTime endDate)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO `subscriptions`(userid, itemId, type, description, startdate, enddate)
VALUES({userId}, {itemId}, {type}, {description}, {startDate}, {endDate})")
                .ConfigureAwait(false);
        }

        public async Task RemoveSubscriptionAsync(int id)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
DELETE FROM `subscriptions`
WHERE id={id}")
                .ConfigureAwait(false);
        }

        public IEnumerable<Subscriptions> GetExpiredSubscriptions()
        {
            return Set.Where(s => s.EndDate <= DateTime.UtcNow).AsEnumerable().ToList();
        }

        public async Task<int> CheckSubscription(ulong userId, int itemId)
        {
            var sub = await Set.FirstOrDefaultAsync(s => s.UserId == userId && s.ItemId == itemId).ConfigureAwait(false);
            return sub?.Id ?? 0;
        }

        public async Task UpdateSubscriptionsAsync(int id, int days)
        {
            var sub = await Set.FirstOrDefaultAsync(s => s.Id == id).ConfigureAwait(false);
            var newEndDate = sub.EndDate + TimeSpan.FromDays(days);
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE `subscriptions`
SET enddate = {newEndDate}
WHERE id = {id} ")
                .ConfigureAwait(false);
        }

        public List<Subscriptions> GetUserSubscriptions(ulong userId)
        {
            return Set.Where(s => s.UserId == userId).ToList();
        }

        public bool DoubleXpIsActive(ulong userId)
        {
            return GetUserSubscriptions(userId).Any(s => s.Description.Contains("double", StringComparison.OrdinalIgnoreCase));
        }

        public bool FastXpIsActive(ulong userId)
        {
            return GetUserSubscriptions(userId).Any(s => s.Description.Contains("fast", StringComparison.OrdinalIgnoreCase));
        }
    }
}