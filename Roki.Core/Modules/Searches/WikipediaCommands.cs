using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Searches.Models;
using Roki.Modules.Searches.Services;

namespace Roki.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class WikipediaCommands : RokiSubmodule<WikipediaService>
        {
            private const string WikipediaIconUrl = "https://i.imgur.com/UA0wMvt.png";
            private const string WikipediaUrl = "https://en.wikipedia.org/wiki";
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task Wikipedia([Leftover] string args)
            {
                var argsSplit = args.Split(' ');
                var queryBuilder = new StringBuilder();
                var showResults = false;
                foreach (var str in argsSplit)
                {
                    if (!showResults && str.Equals("-q", StringComparison.OrdinalIgnoreCase))
                    {
                        showResults = true;
                        continue;
                    }

                    queryBuilder.Append(str + " ");
                }

                var query = queryBuilder.ToString().Trim();
                using var typing = Context.Channel.EnterTypingState();
                // maybe in future only get 1 article if -q not provided
                var results = await Service.SearchAsync(query).ConfigureAwait(false);
                if (results == null || results.Count == 0)
                {
                    await Context.Channel.SendErrorAsync($"Cannot find any results for: `{query}`").ConfigureAwait(false);
                    return;
                }
                if (string.IsNullOrWhiteSpace(results[0].Snippet))
                {
                    await Context.Channel.SendErrorAsync($"Cannot find any results for: `{query}`, did you mean `{results[0].Title}`?");
                    return;
                }
                
                if (!showResults)
                {
                    var article = await Service.GetSummaryAsync(results[0].Title).ConfigureAwait(false);
                    await SendArticleAsync(article).ConfigureAwait(false);
                    return;
                }

                var counter = 1;
                var embed = new EmbedBuilder().WithOkColor()
                    .WithAuthor("Wikipedia", WikipediaIconUrl)
                    .WithTitle($"Search results for: `{query}`")
                    .WithDescription(string.Join("\n", results
                        .Select(a => $"{counter++}. [{a.Title}]({WikipediaUrl}/{HttpUtility.UrlPathEncode(a.Title)})\n\t{a.Snippet}")));

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                // for future allow selecting article and showing it
            }

            private async Task SendArticleAsync(WikiSummary article)
            {
                var embed = new EmbedBuilder().WithOkColor()
                    .WithAuthor("Wikipedia", WikipediaIconUrl)
                    .WithTitle(article.Title)
                    .WithUrl($"{WikipediaUrl}/{HttpUtility.UrlPathEncode(article.Title)}")
                    .WithDescription(article.Extract);
                
                if (!string.IsNullOrWhiteSpace(article.ImageUrl))
                {
                    embed.WithImageUrl(article.ImageUrl);
                }

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }
    }
}