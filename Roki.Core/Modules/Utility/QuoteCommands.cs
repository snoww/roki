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
                page -= 1;
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
                    await ctx.Channel.SendErrorAsync("No quotes found on that page.").ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ShowQuote([Leftover] string keyword)
            {
                if (string.IsNullOrWhiteSpace(keyword))
                    return;

                keyword = keyword.ToUpperInvariant();

                Quote quote;
                using (var uow = _db.GetDbContext())
                {
                    quote = await uow.Quotes.GetRandomQuoteByKeywordAsync(ctx.Guild.Id, keyword);
                }
                
                if (quote == null)
                    return;
                // TODO make quotes look better
                await ctx.Channel.SendMessageAsync($"`#{quote.Id}` 📣 " + quote.Text).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task AddQuote(string keyword, [Leftover] string text)
            {
                if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
                    return;

                keyword = keyword.ToUpperInvariant();

                using (var uow = _db.GetDbContext())
                {
                    uow.Quotes.Add(new Quote
                    {
                        AuthorId = ctx.Message.Author.Id,
                        AuthorName = ctx.Message.Author.Username,
                        GuildId = ctx.Guild.Id,
                        Keyword = keyword,
                        Text = text,
                    });
                    await uow.SaveChangesAsync();
                }

                await ctx.Channel.SendMessageAsync("Quote added.");
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task QuoteDelete(int id)
            {
                var isAdmin = ((IGuildUser) ctx.Message.Author).GuildPermissions.Administrator;
                var success = false;
                string response;

                using (var uow = _db.GetDbContext())
                {
                    var q = uow.Quotes.GetById(id);

                    if (q?.GuildId != ctx.Guild.Id || !isAdmin && q.AuthorId != ctx.Message.Author.Id)
                    {
                        response = "No quotes found which you can remove.";
                    }
                    else
                    {
                        uow.Quotes.Remove(q);
                        await uow.SaveChangesAsync();
                        success = true;
                        response = $"Quote #{id} deleted";
                    }
                }

                if (success)
                    await ctx.Channel.SendConfirmAsync(response).ConfigureAwait(false);
                await ctx.Channel.SendErrorAsync(response).ConfigureAwait(false);
            }
            
        }
    }
    
}