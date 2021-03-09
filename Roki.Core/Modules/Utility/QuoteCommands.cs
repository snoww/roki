using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;

namespace Roki.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        [RequireContext(ContextType.Guild)]
        public class QuoteCommands : RokiSubmodule
        {
            private readonly IRokiDbService _dbService;
            private readonly IConfigurationService _config;

            public QuoteCommands(IRokiDbService dbService, IConfigurationService config)
            {
                _dbService = dbService;
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

                List<Quote> quotes = await _dbService.Context.Quotes.AsAsyncEnumerable()
                    .Where(x => x.GuildId == Context.Guild.Id)
                    .OrderBy(x => x.Id)
                    .Skip(page * 15)
                    .Take(15)
                    .ToListAsync();
                
                if (quotes.Count > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithTitle("Quote List")
                            .WithDescription(string.Join("\n", quotes.Select(x => $"`{x.Id}` **{x.Keyword}** by {Context.Guild.GetUserAsync(x.AuthorId).Result}"))))
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
                string quote = await _dbService.Context.Quotes.AsAsyncEnumerable()
                    .Where(x => x.Keyword == keyword.ToUpper() && x.GuildId == Context.Guild.Id)
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(1)
                    .Select(x => x.Text)
                    .SingleOrDefaultAsync();
                
                if (string.IsNullOrEmpty(quote))
                {
                    return;
                }

                await Context.Channel.SendMessageAsync(quote).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task QuoteId(long id)
            {
                string quote = await _dbService.Context.Quotes.AsAsyncEnumerable()
                    .Where(x => x.Id == id && x.GuildId == Context.Guild.Id)
                    .Select(x => x.Text)
                    .SingleOrDefaultAsync();
                
                if (string.IsNullOrEmpty(quote))
                {
                    return;
                }

                await Context.Channel.SendMessageAsync(quote).ConfigureAwait(false);
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
                
                var quote = new Quote
                {
                    AuthorId = Context.User.Id,
                    GuildId = Context.Guild.Id,
                    Keyword = keyword.ToUpper(),
                    Context = $"https://discordapp.com/channels/{Context.Guild.Id}/{Context.Channel.Id}/{Context.Message.Id}",
                    Text = text
                };

                await _dbService.Context.Quotes.AddAsync(quote);
                await _dbService.SaveChangesAsync();

                await Context.Channel.SendMessageAsync($"Quote `{quote.Id}` **{keyword}** added.").ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task QuoteDeleteId(long id)
            {
                bool isAdmin = ((IGuildUser) Context.User).GuildPermissions.Administrator;

                Quote quote;
                if (isAdmin)
                {
                     quote = await _dbService.Context.Quotes.AsAsyncEnumerable().SingleOrDefaultAsync(x => x.Id == id && x.GuildId == Context.Guild.Id);
                }
                else
                {
                    quote = await _dbService.Context.Quotes.AsAsyncEnumerable().SingleOrDefaultAsync(x => x.Id == id && x.GuildId == Context.Guild.Id && x.AuthorId == Context.User.Id);
                }

                if (quote != null)
                {
                    _dbService.Context.Quotes.Remove(quote);
                    await _dbService.SaveChangesAsync();
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

                Quote quote;
                if (isAdmin)
                {
                    quote = await _dbService.Context.Quotes.AsAsyncEnumerable().SingleOrDefaultAsync(x => x.Keyword == keyword.ToUpper() && x.GuildId == Context.Guild.Id);
                }
                else
                {
                    quote = await _dbService.Context.Quotes.AsAsyncEnumerable().SingleOrDefaultAsync(x => x.Keyword == keyword.ToUpper() && x.GuildId == Context.Guild.Id && x.AuthorId == Context.User.Id);
                }

                if (quote != null)
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
                {
                    return;
                }

                List<Quote> quotes = await _dbService.Context.Quotes.AsAsyncEnumerable()
                    .Where(x => x.GuildId == Context.Guild.Id && 
                        (EF.Functions.ILike(x.Keyword, $"%{query}%") 
                         || EF.Functions.ILike(x.Text, $"%{query}%")))
                    .Take(15)
                    .ToListAsync();
                
                // todo maybe paginate
                if (quotes.Count > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithTitle($"Quote Search Results for `{query}`")
                            .WithDescription(string.Join("\n", quotes.Select(x => $"`{x.Id}` **{x.Keyword}**: {x.Text.TrimTo(50)}"))))
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