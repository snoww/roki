using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Stocks.Extensions;
using Roki.Modules.Stocks.Models;
using Roki.Modules.Stocks.Services;

namespace Roki.Modules.Stocks
{
    public partial class Stocks : RokiTopLevelModule<StocksService>
    {
        private static readonly string TempDir = Path.GetTempPath();
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task StockCompany(string symbol)
        {
            symbol = symbol.ParseStockTicker();
            Company company = await Service.GetCompanyAsync(symbol).ConfigureAwait(false);
            if (company == null)
            {
                await Context.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }
            
            string logo = await Service.GetLogoAsync(symbol).ConfigureAwait(false);

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle(company.CompanyName)
                    .WithAuthor(company.Symbol, logo, company.Website)
                    .WithDescription(company.Description.TrimTo(2048))
                    .AddField("Primary Exchange", company.Exchange, true)
                    .AddField("CEO", company.Ceo, true)
                    .AddField("Employees", company.Employees, true)
                    .AddField("Country", company.Country, true)
                    .AddField("Tags", string.Join(", ", company.Tags))
                )
                .ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task StockNews(string symbol)
        {
            StockNews[] news = await Service.GetNewsAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
            if (news == null)
            {
                await Context.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }

            await Context.SendPaginatedMessageAsync(0, p =>
            {
                StockNews article = news[p];
                EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle(article.Headline)
                    .WithAuthor(article.Source, url: article.Url)
                    .WithDescription($"{article.Summary}\nSource: [{article.Source}]({article.Url})".TrimTo(2048))
                    .WithImageUrl(article.Image)
                    .WithFooter($"{article.DateTime.ToEasternStandardTime():yyyy-MM-dd h:mm:ss tt} EST");
                if (article.HasPayWall)
                    embed.WithDescription("NOTE: article has paywall\n\n" + article.Summary);
                return embed;
            }, news.Length, 1, false).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task StockQuote(string symbol)
        {
            symbol = symbol.ParseStockTicker();
            Quote quote = await Service.GetQuoteAsync(symbol).ConfigureAwait(false);
            if (quote == null)
            {
                await Context.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }
            string logo = await Service.GetLogoAsync(symbol).ConfigureAwait(false);

            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithAuthor(quote.Symbol, logo)
                .WithTitle(quote.CompanyName)
                .AddField("Primary Exchange", quote.PrimaryExchange)
                .AddField("Latest Price", quote.LatestPrice.ToString("N2"))
                .AddField("Change", quote.Change.ToString("N2"), true)
                .AddField("Change %", quote.ChangePercent.ToString("P2"), true)
                .WithFooter($"Latest Time {quote.LatestUpdate.ToEasternStandardTime():yyyy-MM-dd h:mm:ss tt} EST");
            if (quote.Open != null)
                embed.AddField("Open", quote.Open.Value.ToString("N2"), true);
            if (quote.Close != null)
                embed.AddField("Close", quote.Close.Value.ToString("N2"), true);
            if (quote.High != null)
                embed.AddField("High", quote.High.Value.ToString("N2"), true);
            if (quote.Low != null)
                embed.AddField("Low", quote.Low.Value.ToString("N2"), true);
            
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Usage, Description, Aliases]
        public async Task StockPrice(string symbol)
        {
            symbol = symbol.ParseStockTicker();
            Quote quote = await Service.GetQuoteAsync(symbol).ConfigureAwait(false);
            if (quote == null)
            {
                await Context.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }
            string logo = await Service.GetLogoAsync(symbol).ConfigureAwait(false);


            var sb = new StringBuilder();
            sb.AppendLine("```diff")
                .Append("Price: ")
                .AppendLine(quote.LatestPrice.ToString("N2"));
            if (quote.Change >= 0)
            {
                sb.AppendLine("Change").Append('+').AppendLine(quote.Change.ToString("N2"))
                    .AppendLine("Change %").Append('+').AppendLine(quote.ChangePercent.ToString("P2"));
            }
            else
            {
                sb.AppendLine("Change").AppendLine(quote.Change.ToString("N2"))
                    .AppendLine("Change %")
                    .AppendLine(quote.ChangePercent.ToString("P2"));
            }

            sb.Append("```");

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle(quote.CompanyName)
                    .WithAuthor(symbol, logo)
                    .WithDescription(sb.ToString())
                    .AddField("Last Updated", $"{quote.LatestUpdate.ToEasternStandardTime():yyyy-MM-dd h:mm:ss tt} EST")
                    .WithFooter(quote.LatestSource))
                .ConfigureAwait(false);
        }
    }
}