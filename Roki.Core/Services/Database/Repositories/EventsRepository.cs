using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IEventsRepository : IRepository<Event>
    {
        
    }
    
    public class EventsRepository : Repository<Event>, IEventsRepository
    {
        public EventsRepository(DbContext context) : base(context)
        {
        }
    }
}