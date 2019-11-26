using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IMessageRepository: IRepository<Message>
    {
        void MessageDeleted(ulong messageId);
        Message GetMessageById(int id);
    }

    public class MessageRepository : Repository<Message>, IMessageRepository
    {
        public MessageRepository(DbContext context) : base(context)
        {
        }

        public void MessageDeleted(ulong messageId)
        {
            Set.Where(m => m.MessageId == messageId)
                .AsNoTracking()
                .ToList()
                .ForEach(m => m.IsDeleted = true);;
        }

        public Message GetMessageById(int id)
        {
            return Set.Last(m => m.Id == id);
        }
    }
}