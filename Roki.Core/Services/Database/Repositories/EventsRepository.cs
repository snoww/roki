using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IEventsRepository : IRepository<Events>
    {
        
    }
    
    public class EventsRepository : Repository<Events>, IEventsRepository
    {
        public EventsRepository(DbContext context) : base(context)
        {
        }
    }
}