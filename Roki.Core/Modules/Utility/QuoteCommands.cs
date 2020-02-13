using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MongoDB.Bson;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Core;
using Quote = Roki.Services.Database.Maps.Quote;

namespace Roki.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class QuoteCommands : RokiSubmodule
        {
            private readonly IMongoService _mongo;
            
            public QuoteCommands(IMongoService mongo)
            {
                _mongo = mongo;
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ListQuotes(int page = 1, OrderType order = OrderType.Keyword)
            {
                page -= 1;
                if (page < 0)
                    return;

                using (var uow = _db.GetDbContext())
                {
                    var quotes = uow.Quotes.GetGroup(Context.Guild.Id, page, order).ToList();
                    if (quotes.Count > 0)
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                                .WithTitle("Quote List")
                                .WithDescription(string.Join("\n", quotes.Select(q => $"`#{q.Id}` {Format.Bold(q.Keyword),-20} by {q.AuthorName}"))))
                            .ConfigureAwait(false);
                    }
                    else
                        await Context.Channel.SendErrorAsync("No quotes found on that page.").ConfigureAwait(false);
                }
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ShowQuote(string keyword, bool context = false)
            {
                if (string.IsNullOrWhiteSpace(keyword))
                    return;

                keyword = keyword.ToUpperInvariant();

                var quote = await _mongo.Context.GetRandomQuoteByKeyAsync(Context.Guild.Id, keyword).ConfigureAwait(false)
                if (quote == null)
                    return;
                
                var author = await Context.Guild.GetUserAsync(quote.AuthorId).ConfigureAwait(false);
                if (context)
                {
                    await Context.Channel.SendMessageAsync($"`#{quote.Id.Pid}` by `{author}`. Use count: `{quote.UseCount}`\n📣 {quote.Text}\nContext: {quote.Context}").ConfigureAwait(false);
                    return;
                }
                await Context.Channel.SendMessageAsync($"`#{quote.Id.Pid}` by `{author}`. Use count: `{quote.UseCount}`\n📣 {quote.Text}").ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task AddQuote(string keyword, [Leftover] string text)
            {
                if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
                    return;

                keyword = keyword.ToUpperInvariant();

                var quote = new Quote
                {
                    Id = ObjectId.GenerateNewId(),
                    AuthorId = Context.Message.Author.Id,
                    GuildId = Context.Guild.Id,
                    Keyword = keyword,
                    Context = $"https://discordapp.com/channels/{Context.Guild.Id}/{Context.Channel.Id}/{Context.Message.Id}",
                    Text = text
                };
                
                await _mongo.Context.AddQuoteAsync(quote);

                await Context.Channel.SendMessageAsync($"Quote `{quote.Id.Pid}` added.").ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task QuoteDelete(int id)
            {
                var isAdmin = ((IGuildUser) Context.Message.Author).GuildPermissions.Administrator;

                using (var uow = _db.GetDbContext())
                {
                    var quote = uow.Context.Quotes.First(q => q.Id == id);

                    if (quote.GuildId != Context.Guild.Id || !isAdmin && quote.AuthorId != Context.Message.Author.Id)
                    {
                        await Context.Channel.SendErrorAsync("No quotes found which you can remove.").ConfigureAwait(false);
                    }
                    else
                    {
                        uow.Quotes.Remove(quote);
                        await uow.SaveChangesAsync().ConfigureAwait(false);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                                .WithDescription($"Quote #{id} deleted"))
                            .ConfigureAwait(false);
                    }
                }
            }

            /*[RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task QuoteSearch(string keyword, [Leftover] string text)
            {
                if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
                    return;

                keyword = keyword.ToUpperInvariant();

                Quote quote;
                using (var uow = _db.GetDbContext())
                {
                    quote = await uow.Quotes.SearchQuoteKeywordTextAsync(ctx.Guild.Id, keyword, text);
                }

                if (quote == null)
                    return;

                await ctx.Channel.SendMessageAsync($"`#{quote.Id}` 💬 " + keyword.ToLowerInvariant() + ": " + quote.Text).ConfigureAwait(false);
            }*/

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task QuoteId(int id)
            {
                if (id < 0)
                    return;

                Quote quote;
                using (var uow = _db.GetDbContext())
                {
                    quote = uow.Context.Quotes.First(q => q.Id == id);
                    if (quote.GuildId != Context.Guild.Id)
                        quote = null;
                    await uow.Quotes.IncrementUseCount(id).ConfigureAwait(false);
                }

                if (quote == null)
                {
                    await Context.Channel.SendErrorAsync("Quote not found.").ConfigureAwait(false);
                    return;
                }

                await Context.Channel.SendMessageAsync($"`#{quote.Id}` added by {quote.AuthorName} 🗯️ " + quote.Keyword.ToLowerInvariant() + "\n")
                    .ConfigureAwait(false);
            }
        }
    }
}