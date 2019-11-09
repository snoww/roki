using System.Threading.Tasks;
using Discord;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Stocks.Services;

namespace Roki.Modules.Stocks
{
    public partial class Stocks : RokiTopLevelModule<StocksService>
    {
        [RokiCommand, Usage, Description, Aliases]
        public async Task StockStats(string symbol, [Leftover] string all = "false")
        {
            var stats = await _service.GetStockStatsAsync(symbol).ConfigureAwait(false);
            var logo = await _service.GetLogoAsync(symbol).ConfigureAwait(false);
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
                    .WithTitle($"{stats.CompanyName} - {symbol.ToUpperInvariant()}")
                    .WithThumbnailUrl(logo)
                    .WithDescription(desc))
                .ConfigureAwait(false);
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Company(string symbol)
        {
            var company = await _service.GetCompanyAsync(symbol).ConfigureAwait(false);
            var logo = await _service.GetLogoAsync(symbol).ConfigureAwait(false);
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
            var news = await _service.GetNewsAsync(symbol).ConfigureAwait(false);
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
            var quote = await _service.GetQuoteAsync(symbol).ConfigureAwait(false);
            var logo = await _service.GetLogoAsync(symbol).ConfigureAwait(false);
            if (quote == null)
            {
                await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle($"{quote.CompanyName} - {quote.Symbol}")
                .WithThumbnailUrl(logo)
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
            var price = await _service.GetLatestPriceAsync(symbol).ConfigureAwait(false);
            if (price == null)
            {
                await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle($"Latest Price for {symbol.ToUpperInvariant()}\n`{price:N2}`"))
                .ConfigureAwait(false);
        }
    }
}