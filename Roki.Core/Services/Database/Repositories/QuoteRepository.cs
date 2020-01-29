using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Extensions;
using Roki.Services.Database.Core;

namespace Roki.Services.Database.Repositories
{
    public interface IQuoteRepository : IRepository<Quote>
    {
        Task<Quote> GetRandomQuoteByKeywordAsync(ulong guildId, string keyword);
        Task<Quote> SearchQuoteKeywordTextAsync(ulong guildId, string keyword, string text);
        IEnumerable<Quote> GetGroup(ulong guildId, int page, OrderType order);
        Task IncrementUseCount(int id);
        void RemoveAllByKeyword(ulong guildId, string keyword);
    }
    
    public class QuoteRepository : Repository<Quote>, IQuoteRepository
    {
        public QuoteRepository(DbContext context) : base(context)
        {
        }

        public async Task<Quote> GetRandomQuoteByKeywordAsync(ulong guildId, string keyword)
        {
            var rand = new Random();
            var quotes = Set.Where(q => q.GuildId == guildId && q.Keyword == keyword).ToList();
            var quote = quotes.OrderBy(q => rand.Next()).FirstOrDefault();
            if (quote != null) quote.UseCount += 1;
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return quote;
        }

        public Task<Quote> SearchQuoteKeywordTextAsync(ulong guildId, string keyword, string text)
        {
            var rand = new Random();
            return Set.Where(q => q.Text.Contains(text, StringComparison.OrdinalIgnoreCase) && q.GuildId == guildId && q.Keyword == keyword)
                .OrderBy(q => rand.Next())
                .FirstOrDefaultAsync();
        }

        public IEnumerable<Quote> GetGroup(ulong guildId, int page, OrderType order)
        {
            var q = Set.Where(x => x.GuildId == guildId);
            q = order == OrderType.Keyword ? q.OrderBy(x => x.Keyword) : q.OrderBy(x => x.Id);

            return q.Skip(15 * page).Take(15).ToArray();
        }

        public async Task IncrementUseCount(int id)
        {
            var quote = await Set.FirstAsync(q => q.Id == id).ConfigureAwait(false);
            quote.UseCount += 1;
            await Context.SaveChangesAsync().ConfigureAwait(false);
        }

        public void RemoveAllByKeyword(ulong guildId, string keyword)
        {
            Set.RemoveRange(Set.Where(x => x.GuildId == guildId && x.Keyword.ToUpperInvariant() == keyword));
        }
    }
}