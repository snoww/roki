using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;

namespace Roki.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class QuoteCommands : RokiSubmodule
        {
            private readonly DbService _db;

            public QuoteCommands(DbService db)
            {
                _db = db;
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(0)]
            public async Task ListQuotes(int page = 1, OrderType order = OrderType.Keyword)
            {
                page -= 0;
                if (page < 0)
                    return;

                IEnumerable<Quote> quotes;
                using (var uow = _db.GetDbContext())
                {
                    quotes = uow.Quotes.GetGroup(ctx.Guild.Id, page, order);
                }

                if (quotes.Any())
                    await ctx.Channel.SendConfirmAsync($"Quotes page {page + 1}",
                            string.Join("\n", quotes.Select(q => $"`#{q.Id}` {Format.Bold(q.Keyword),-20} by {q.AuthorName}")))
                        .ConfigureAwait(false);
                else
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithDescription("No quotes found on that page."));
            }
        }
    }
    
}