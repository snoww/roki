using System;
using Discord;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IDMessageRepository: IRepository<DMessage>
    {
        void AddMessage(IMessage message);
        void UpdateMessage(IMessage message);
    }

    public class DMessageRepository : Repository<DMessage>, IDMessageRepository
    {
        public DMessageRepository(DbContext context) : base(context)
        {
        }

        public void AddMessage(IMessage message)
        {
            Context.Database.ExecuteSqlCommand($@"
INSERT IGNORE INTO Messages (Id, AuthorId, Author, ChannelId, Channel, GuildId, Guild, MessageId, Content, EditedTimestamp, Timestamp)
VALUES ({message.Author.Id}, {message.Author.Username}, {message.Channel.Id}, {message.Channel.Name}, {(message.Channel as ITextChannel)?.GuildId}, {(message.Channel as ITextChannel)?.Guild.Name}, {message.EditedTimestamp?.UtcDateTime}, {message.Timestamp.UtcDateTime});
");
        }

        public void UpdateMessage(IMessage message)
        {
            throw new NotImplementedException();
        }
    }
}