using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface ISubscriptionRepository : IRepository<Subscriptions>
    {
        Task NewSubscription(ulong userId, string description, DateTime startDate, DateTime endDate);
    }
    
    public class SubscriptionRepository : Repository<Subscriptions>, ISubscriptionRepository
    {
        public SubscriptionRepository(DbContext context) : base(context)
        {
        }


        public async Task NewSubscription(ulong userId, string description, DateTime startDate, DateTime endDate)
        {
            await Context.Database.ExecuteSqlCommandAsync($@"
INSERT INTO `subscriptions`(userid, description, startdate, enddate)
VALUES({userId}, {description}, {startDate}, {endDate})")
                .ConfigureAwait(false);
        }
    }
}