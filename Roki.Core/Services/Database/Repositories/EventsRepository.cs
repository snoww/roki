using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IEventsRepository : IRepository<Event>
    {
        List<Event> GetAllActiveEvents();
    }
    
    public class EventsRepository : Repository<Event>, IEventsRepository
    {
        public EventsRepository(DbContext context) : base(context)
        {
        }

        public List<Event> GetAllActiveEvents()
        {
            return Set.Where(e => true).ToList();
        }
    }
}