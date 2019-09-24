using System;
using System.Linq;
using Discord;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IDMessageRepository: IRepository<DMessage>
    {
        DMessage GetMessageById(int id);
    }

    public class DMessageRepository : Repository<DMessage>, IDMessageRepository
    {
        public DMessageRepository(DbContext context) : base(context)
        {
        }

        public DMessage GetMessageById(int id)
        {
            return Set.Last(m => m.Id == id);
        }
    }
}