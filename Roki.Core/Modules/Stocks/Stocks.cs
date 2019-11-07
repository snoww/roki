using System.Threading.Tasks;
using Discord;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Stocks.Services;

namespace Roki.Modules.Stocks
{
    public class Stocks : RokiTopLevelModule<StocksService>
    {
        [RokiCommand, Usage, Description, Aliases]
        public async Task StocksStats(string symbol, string all = "false")
        {
            var stats = await _service.GetStockStatsAsync(symbol);
            var logo = await _service.GetLogoAsync(symbol);
            if (stats == null)
            {
                await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                return;
            }

            string desc;

            if (all != "false")
            {
                desc = $"```Market Cap\n{stats.MarketCap:N0}\n\n52 Week High\n{stats.Week52High:N}\n\n52 Week Low\n{stats.Week52Low:N}\n\n52 Week Change\n{stats.Week52Change:P}\n\nShares Outstanding\n{stats.SharesOutstanding:N}\n\nAverage 30 Day Volume\n{stats.Avg30Volume:N1}\n\nAverage 10 Day Volume\n{stats.Avg10Volume:N1}\n\nFloat\n{stats.Float:N3}\n\nEmployees\n{stats.Employees:N0}\n\nTrailing 12 Month Earnings Per Share\n{stats.TtmEps}\n\nNext Earnings Date\n{stats.NextEarningsDate:yyyy-MM-dd}\n\nPrice to Earnings Ratio\n{stats.PeRatio}\n\nBeta\n{stats.Beta}```";
            }
            else
            {
                desc = $"```Market Cap\n{stats.MarketCap:N0}\n\n52 Week High\n{stats.Week52High:N}\n\n52 Week Low\n{stats.Week52Low:N}\n\n52 Week Change\n{stats.Week52Change:P}\n\nShares Outstanding\n{stats.SharesOutstanding:N}\n\nAverage 30 Day Volume\n{stats.Avg30Volume:N1}\n\nAverage 10 Day Volume\n{stats.Avg10Volume:N1}\n\nFloat\n{stats.Float:N3}\n\nEmployees\n{stats.Employees:N0}\n\nTrailing 12 Month Earnings Per Share\n{stats.TtmEps}\n\nNext Earnings Date\n{stats.NextEarningsDate:yyyy-MM-dd}\n\nPrice to Earnings Ratio\n{stats.PeRatio}\n\nBeta\n{stats.Beta}```";
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
            
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task News(string symbol)
        {
            
        }
    }
}