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
        Task NewSubscriptionAsync(ulong userId, string description, DateTime startDate, DateTime endDate);
        Task RemoveSubscriptionAsync(int id);
        List<Subscriptions> GetExpiredSubscriptions();
    }
    
    public class SubscriptionRepository : Repository<Subscriptions>, ISubscriptionRepository
    {
        public SubscriptionRepository(DbContext context) : base(context)
        {
        }


        public async Task NewSubscriptionAsync(ulong userId, string description, DateTime startDate, DateTime endDate)
        {
            await Context.Database.ExecuteSqlCommandAsync($@"
INSERT INTO `subscriptions`(userid, description, startdate, enddate)
VALUES({userId}, {description}, {startDate}, {endDate})")
                .ConfigureAwait(false);
        }

        public async Task RemoveSubscriptionAsync(int id)
        {
            await Context.Database.ExecuteSqlCommandAsync($@"
DELETE FROM `subscriptions`
WHERE id={id}")
                .ConfigureAwait(false);
        }

        public List<Subscriptions> GetExpiredSubscriptions()
        {
            return Set.Where(s => s.EndDate <= DateTime.UtcNow).AsEnumerable().ToList();
        }
    }
}