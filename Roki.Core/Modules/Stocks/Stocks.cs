using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Stocks.Extensions;
using Roki.Modules.Stocks.Services;

namespace Roki.Modules.Stocks
{
    public partial class Stocks : RokiTopLevelModule<StocksService>
    {
        [RokiCommand, Usage, Description, Aliases]
        public async Task StockStats(string symbol, [Leftover] string all = "false")
        {
            var stats = await _service.GetStockStatsAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
            var logo = await _service.GetLogoAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
            if (stats == null)
            {
                await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }

            string desc;
            if (all == "false")
            {
                desc = $"Market Cap:\t`{stats.MarketCap:N0}`\n52 Week High:\t`{stats.Week52High:N2}`\n52 Week Low:\t`{stats.Week52Low:N2}`\n52 Week Change:\t`{stats.Week52Change:P}`\nShares Outstanding:\t`{stats.SharesOutstanding:N2}`\nAverage 30 Day Volume:\t`{stats.Avg30Volume:N1}`\nAverage 10 Day Volume:\t`{stats.Avg10Volume:N1}`\nFloat:\t`{stats.Float:N3}`\nEmployees:\t`{stats.Employees:N0}`\nTrailing 12 Month Earnings Per Share:\t`{stats.TtmEps}`\nNext Earnings Date:\t`{stats.NextEarningsDate:yyyy-MM-dd}`\nPrice to Earnings Ratio:\t`{stats.PeRatio}`\nBeta:\t`{stats.Beta}`";
            }
            else
            {
                desc = $"Market Cap:\t`{stats.MarketCap:N0}`\n52 Week High:\t`{stats.Week52High:N2}`\n52 Week Low:\t`{stats.Week52Low:N2}`\n52 Week Change:\t`{stats.Week52Change:P}`\nShares Outstanding:\t`{stats.SharesOutstanding:N2}`\nAverage 30 Day Volume:\t`{stats.Avg30Volume:N1}`\nAverage 10 Day Volume:\t`{stats.Avg10Volume:N1}`\nFloat:\t`{stats.Float:N3}`\nEmployees:\t`{stats.Employees:N0}`\nTrailing 12 Month Earnings Per Share:\t`{stats.TtmEps}`\nNext Earnings Date:\t`{stats.NextEarningsDate:yyyy-MM-dd}`\nPrice to Earnings Ratio:\t`{stats.PeRatio}`\nBeta:\t`{stats.Beta}`" +
                       "`===============`" + 
                       $"5 Day Change:\t`{stats.Day5ChangePercent:P}`\n30 Day Change:\t`{stats.Day30ChangePercent:P}`\n1 Month Change:\t`{stats.Month1ChangePercent:P}`\n3 Month Change:\t`{stats.Month3ChangePercent:P}`\n6 Month Change:\t`{stats.Month6ChangePercent:P}`\nYTD Change:\t`{stats.YtdChangePercent:P}`\n1 year Change:\t`{stats.Year1ChangePercent:P}`\n1 Year Change:\t`{stats.Year1ChangePercent:P}`\n2 Year Change:\t`{stats.Year2ChangePercent:P}`\n5 Year Change:\t`{stats.Year1ChangePercent:P}`\nMax Change:\t`{stats.MaxChangePercent:P}`";
            }
            
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithAuthor(symbol.ToUpper(), logo)
                    .WithTitle(stats.CompanyName)
                    .WithDescription(desc))
                .ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Company(string symbol)
        {
            var company = await _service.GetCompanyAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
            var logo = await _service.GetLogoAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
            if (company == null)
            {
                await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle(company.Symbol)
                    .WithAuthor(company.CompanyName, logo, company.Website)
                    .WithDescription(company.Description)
                    .AddField("Primary Exchange", company.Exchange, true)
                    .AddField("CEO", company.Ceo, true)
                    .AddField("Tags", string.Join(", ", company.Tags))
                    .AddField("Employees", company.Employees, true)
                    .AddField("Country", company.Country, true))
                .ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task News(string symbol)
        {
            var news = await _service.GetNewsAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
            if (news == null)
            {
                await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }

            await ctx.SendPaginatedConfirmAsync(0, p =>
            {
                var article = news[p];
                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle(article.Headline)
                    .WithAuthor(article.Source, article.Url)
                    .WithDescription(article.Summary + $"\nSource: [{article.Source}]({article.Url})")
                    .WithImageUrl(article.Image)
                    .WithFooter($"{article.DateTime.UnixTimeStampToDateTime():yyyy-MM-dd HH:mm}");
                if (article.HasPayWall)
                    embed.WithDescription("Has Pay Wall\n" + article.Summary);
                return embed;
            }, news.Length, 1, false).ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task StockQuote(string symbol)
        {
            var quote = await _service.GetQuoteAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
            var logo = await _service.GetLogoAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
            if (quote == null)
            {
                await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor()
                .WithAuthor(quote.Symbol, logo)
                .WithTitle(quote.CompanyName)
                .AddField("Primary Exchange", quote.PrimaryExchange, true)
                .AddField("Latest Price", quote.LatestPrice.ToString("N2"), true)
                .AddField("Latest Time", quote.LatestUpdate.UnixTimeStampToDateTime().ToString("yyyy-MM-dd HH:mm"), true);
            if (quote.Open != null)
                embed.AddField("Open", quote.Open.Value.ToString("N2"), true);
            if (quote.Close != null)
                embed.AddField("Close", quote.Close.Value.ToString("N2"), true);
            if (quote.High != null)
                embed.AddField("High", quote.High.Value.ToString("N2"), true);
            if (quote.Low != null)
                embed.AddField("Low", quote.Low.Value.ToString("N2"), true);
            
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Usage, Description, Aliases]
        public async Task StockPrice(string symbol)
        {
            var price = await _service.GetQuoteAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
            if (price == null)
            {
                await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle($"Latest Price for {symbol.ToUpperInvariant()}\n`{price.LatestPrice:N2}`")
                    .WithFooter($"Last Updated: {price.LatestTime}"))
                .ConfigureAwait(false);
        }

        [RokiCommand, Usage, Description, Aliases]
        public async Task StockChart(string symbol, string period = "1m")
        {
            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var options = new Dictionary<string, string>
            {
                {"max", "Max"},
                {"5y", "5 Year"},
                {"2y", "2 Year"},
                {"1y", "1 Year"},
                {"ytd", "Year to Date"},
                {"6m", "6 Month"},
                {"3m", "3 Month"},
                {"1m", "1 Month"},
                {"5dm", "5 Day"},
                {"5d", "5 Day"},
                {"today", "Today's"},
            };
            var quote = await _service.GetQuoteAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
            period = period.Trim().ToLower();
            if (quote == null || !options.ContainsKey(period))
            {
                await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false); 
                return;
            }

            if (period.Equals("5d", StringComparison.OrdinalIgnoreCase))
                period = "5dm";
            
            _service.GenerateChartAsync(symbol, period);
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle($"{quote.CompanyName}")
                .WithAuthor(quote.Symbol.ToUpper())
                .WithDescription($"{options[period]} Price")
                .WithImageUrl("attachment://image.png");
            await ctx.Channel.SendFileAsync("./temp/image.png", embed: embed.Build()).ConfigureAwait(false);
            File.Delete("./temp/image.png");
        }
    }
}