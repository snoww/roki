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
        Task <bool> MessageExists(ulong messageId);
        Task MoveToTempTableAsync(ulong messageId);
        Task AddToTempTableAsync(IMessage message);
        Task MoveBackToMessagesAsync(ulong messageId);
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

        public async Task<bool> MessageExists(ulong messageId)
        {
            return await Set.AnyAsync(m => m.MessageId == messageId).ConfigureAwait(false);
        }

        public async Task MoveToTempTableAsync(ulong messageId)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO messages_transfer 
SELECT * from messages
WHERE author_id != 0 AND message_id >= {messageId}").ConfigureAwait(false);
        }

        public async Task AddToTempTableAsync(IMessage message)
        {
            var guild = message.Channel as ITextChannel;
            string content;
            if (!string.IsNullOrWhiteSpace(message.Content) && message.Attachments.Count == 0)
                content = message.Content;
            else if (!string.IsNullOrWhiteSpace(message.Content) && message.Attachments.Count > 0)
                content = message.Content + "\n" + string.Join("\n", message.Attachments.Select(a => a.Url));
            else if (message.Attachments.Count > 0)
                content = string.Join("\n", message.Attachments.Select(a => a.Url));
            else
                content = "";
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO messages_transfer(author_id, author, channel_id, channel, guild_id, guild, message_id, content, edited_timestamp, timestamp)
VALUES ({message.Author.Id}, {message.Author}, {message.Channel.Id}, {message.Channel.Name}, {guild?.Id}, {guild?.Name}, {message.Id}, {content}, {message.EditedTimestamp?.ToUniversalTime()}, {message.Timestamp.ToUniversalTime()})"
            ).ConfigureAwait(false);
        }

        public async Task MoveBackToMessagesAsync(ulong messageId)
        {
            var message = await Set.FirstAsync(m => m.MessageId == messageId).ConfigureAwait(false);
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
DELETE FROM messages
WHERE message_id >= {messageId}
").ConfigureAwait(false);
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
ALTER SEQUENCE messages_id_seq RESTART WITH {message.Id}
").ConfigureAwait(false);
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO messages(author_id, author, channel_id, channel, guild_id, guild, message_id, content, edited_timestamp, timestamp)
SELECT author_id, author, channel_id, channel, guild_id, guild, message_id, content, edited_timestamp, timestamp
FROM messages_transfer 
ORDER BY timestamp
").ConfigureAwait(false);
        }
    }
}