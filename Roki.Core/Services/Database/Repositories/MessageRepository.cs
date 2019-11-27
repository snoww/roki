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
        Task AddMissingMessageAsync(IMessage message);
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

        public async Task AddMissingMessageAsync(IMessage message)
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
            Set.Add(new Message
            {
                AuthorId = message.Author.Id,
                Author = message.Author.Username,
                ChannelId = message.Channel.Id,
                Channel = message.Channel.Name,
                GuildId = message.Channel is ITextChannel chId ? chId.GuildId : (ulong?) null,
                Guild = message.Channel is ITextChannel ch ? ch.Guild.Name : null,
                MessageId = message.Id,
                Content = content,
                EditedTimestamp = message.EditedTimestamp?.ToUniversalTime(),
                Timestamp = message.Timestamp.ToUniversalTime()
            });
            await Context.SaveChangesAsync().ConfigureAwait(false);
        }
        
    }
}