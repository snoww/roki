using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MongoDB.Bson;
using MongoDB.Driver;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        [RequireContext(ContextType.Guild)]
        public class QuoteCommands : RokiSubmodule
        {
            private readonly IMongoService _mongo;
            private readonly IConfigurationService _config;

            public QuoteCommands(IMongoService mongo, IConfigurationService config)
            {
                _mongo = mongo;
                _config = config;
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task ListQuotes(int page = 1)
            {
                page -= 1;
                if (page < 0)
                {
                    page = 0;
                }

                List<Quote> quotes = await _mongo.Context.ListQuotesAsync(Context.Guild.Id, page);
                if (quotes.Count > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithTitle("Quote List")
                            .WithDescription(string.Join("\n", quotes.Select(x => $"`{x.Id.GetHexId()}` **{x.Keyword}** by {Context.Guild.GetUserAsync(x.AuthorId).Result}"))))
                        .ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendErrorAsync("No quotes found on that page.").ConfigureAwait(false);
                }
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task ListQuotesId(int page = 1)
            {
                page -= 1;
                if (page < 0)
                {
                    page = 0;
                }

                List<Quote> quotes = (await _mongo.Context.ListQuotesAsync(Context.Guild.Id, page)).OrderBy(x => x.Id).ToList();

                if (quotes.Count > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithTitle("Quote List")
                            .WithDescription(string.Join("\n", quotes.Select(x => $"`{x.Id.GetHexId()}` **{x.Keyword}** by {Context.Guild.GetUserAsync(x.AuthorId).Result}"))))
                        .ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendErrorAsync("No quotes found on that page.").ConfigureAwait(false);
                }
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task ShowQuote(string keyword)
            {
                keyword = keyword.ToUpperInvariant();

                Quote quote = await _mongo.Context.GetRandomQuoteAsync(Context.Guild.Id, keyword).ConfigureAwait(false);
                if (quote == null)
                {
                    return;
                }

                await Context.Channel.SendMessageAsync(quote.Text).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task QuoteId(string id)
            {
                Quote quote = await _mongo.Context.GetQuoteByIdAsync(Context.Guild.Id, id).ConfigureAwait(false);
                if (quote == null)
                {
                    return;
                }

                await Context.Channel.SendMessageAsync(quote.Text).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task AddQuote(string keyword, [Leftover] string text)
            {
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
                if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
                {
                    await Context.Channel.SendErrorAsync($"You need to include the content of the quote.\n`{guildConfig.Prefix}addquote <quote_name> <quote_content>`")
                        .ConfigureAwait(false);
                    return;
                }

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

                await Context.Channel.SendMessageAsync($"Quote `{quote.Id.GetHexId()}` **{keyword}** added.").ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task QuoteDeleteId(string id)
            {
                bool isAdmin = ((IGuildUser) Context.User).GuildPermissions.Administrator;

                DeleteResult result = isAdmin
                    ? await _mongo.Context.DeleteQuoteAdmin(Context.Guild.Id, id)
                    : await _mongo.Context.DeleteQuoteByIdAsync(Context.Guild.Id, Context.User.Id, id).ConfigureAwait(false);

                if (result.IsAcknowledged && result.DeletedCount > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription($"Quote `{id}` deleted"))
                        .ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendErrorAsync("No quotes found which you can remove.").ConfigureAwait(false);
                }
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task QuoteDelete(string keyword)
            {
                bool isAdmin = ((IGuildUser) Context.User).GuildPermissions.Administrator;

                DeleteResult result = isAdmin
                    ? await _mongo.Context.DeleteQuoteAdmin(Context.Guild.Id, keyword: keyword)
                    : await _mongo.Context.DeleteQuoteAsync(Context.Guild.Id, Context.User.Id, keyword).ConfigureAwait(false);

                if (result.IsAcknowledged && result.DeletedCount > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription($"Quote `{keyword.ToUpperInvariant()}` deleted"))
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
                {
                    return;
                }

                List<Quote> quotes = await _mongo.Context.SearchQuotesByText(Context.Guild.Id, query).ConfigureAwait(false);
                if (quotes.Count > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithTitle("Quote Search Results")
                            .WithDescription(string.Join("\n", quotes.Select(x => $"`{x.Id.GetHexId()}` **{x.Keyword}**: {x.Text.TrimTo(50)}"))))
                        .ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendErrorAsync("No quotes found with that query.").ConfigureAwait(false);
                }
            }
        }
    }
}