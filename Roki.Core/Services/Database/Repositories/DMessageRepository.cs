using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IDMessageRepository: IRepository<DMessage>
    {
        void MessageDeleted(ulong messageId);
        DMessage GetMessageById(int id);
    }

    public class DMessageRepository : Repository<DMessage>, IDMessageRepository
    {
        public DMessageRepository(DbContext context) : base(context)
        {
        }

        public void MessageDeleted(ulong messageId)
        {
            Set.Where(m => m.MessageId == messageId)
                .AsNoTracking()
                .ToList()
                .ForEach(m => m.IsDeleted = true);;
        }

        public DMessage GetMessageById(int id)
        {
            return Set.Last(m => m.Id == id);
        }
    }
}