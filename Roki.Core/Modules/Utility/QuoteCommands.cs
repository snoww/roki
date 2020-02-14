using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MongoDB.Bson;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;

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
            public async Task ListQuotes(int page = 1)
            {
                page -= 1;
                if (page < 0)
                    page = 0;

                var quotes = await _mongo.Context.ListQuotesAsync(Context.Guild.Id, page);
                if (quotes.Count > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithTitle("Quote List")
                            .WithDescription(string.Join("\n", quotes.Select(x => $"`#{x.Id.Increment}` **{x.Keyword}** by {Context.Guild.GetUserAsync(x.AuthorId).Result}"))))
                        .ConfigureAwait(false);
                }
                else
                    await Context.Channel.SendErrorAsync("No quotes found on that page.").ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ShowQuote(string keyword, bool context = false)
            {
                keyword = keyword.ToUpperInvariant();

                var quote = await _mongo.Context.GetRandomQuoteAsync(Context.Guild.Id, keyword).ConfigureAwait(false);
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
            public async Task QuoteId(short id, bool context = false)
            {
                var quote = await _mongo.Context.GetRandomQuoteAsync(Context.Guild.Id, id).ConfigureAwait(false);
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
                    AuthorId = Context.User.Id,
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
            [Priority(1)]
            public async Task QuoteDelete(short id)
            {
                var isAdmin = ((IGuildUser) Context.User).GuildPermissions.Administrator;

                var result = isAdmin
                    ? await _mongo.Context.DeleteQuoteAdmin(Context.Guild.Id, id)
                    : await _mongo.Context.DeleteQuoteAsync(Context.Guild.Id, Context.User.Id, id).ConfigureAwait(false);

                if (result.IsAcknowledged && result.DeletedCount > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription($"Quote `#{id}` deleted"))
                        .ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendErrorAsync("No quotes found which you can remove.").ConfigureAwait(false);
                }
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(0)]
            public async Task QuoteDelete(string keyword)
            {
                var isAdmin = ((IGuildUser) Context.User).GuildPermissions.Administrator;

                var result = isAdmin
                    ? await _mongo.Context.DeleteQuoteAdmin(Context.Guild.Id, keyword: keyword)
                    : await _mongo.Context.DeleteQuoteAsync(Context.Guild.Id, Context.User.Id, keyword).ConfigureAwait(false);

                if (result.IsAcknowledged && result.DeletedCount > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription($"Quote `{keyword.ToUpper()}` deleted"))
                        .ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendErrorAsync("No quotes found which you can remove.").ConfigureAwait(false);
                }
            }

            [RokiCommand, Description, Usage, Aliases, RequireContext(ContextType.Guild)]
            public async Task QuoteSearch([Leftover] string query = null)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;

                var quotes = await _mongo.Context.SearchQuotesByText(Context.Guild.Id, query).ConfigureAwait(false);
                if (quotes.Count > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithTitle("Quote Search Results")
                            .WithDescription(string.Join("\n", quotes.Select(x => $"`#{x.Id.Pid}` **{x.Keyword}**: {x.Text.TrimTo(50)}"))))
                        .ConfigureAwait(false);
                }
                else
                    await Context.Channel.SendErrorAsync("No quotes found with that query.").ConfigureAwait(false);
            }
        }
    }
}