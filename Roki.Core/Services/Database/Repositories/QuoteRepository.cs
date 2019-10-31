using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;

namespace Roki.Core.Services.Database.Repositories
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

        public Task<Quote> GetRandomQuoteByKeywordAsync(ulong guildId, string keyword)
        {
            var rand = new Random();
            return Set.Where(q => q.GuildId == guildId && q.Keyword == keyword).OrderBy(q => rand.Next()).FirstOrDefaultAsync();
        }

        public Task<Quote> SearchQuoteKeywordTextAsync(ulong guildId, string keyword, string text)
        {
            var rand = new Random();
            return Set.Where(q => q.Text.ContainsNoCase(text, StringComparison.OrdinalIgnoreCase) && q.GuildId == guildId && q.Keyword == keyword)
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
            await Context.Database.ExecuteSqlCommandAsync($@"
UPDATE quotes
SET UseCount=UseCount+1
WHERE id={id}")
                .ConfigureAwait(false);
        }

        public void RemoveAllByKeyword(ulong guildId, string keyword)
        {
            Set.RemoveRange(Set.Where(x => x.GuildId == guildId && x.Keyword.ToUpperInvariant() == keyword));
        }
    }
}