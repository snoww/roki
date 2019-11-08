using System.Threading.Tasks;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Stocks.Services;

namespace Roki.Modules.Stocks
{
    public partial class Stocks
    {
        [Group]
        public class TradingCommands : RokiSubmodule<TradingService>
        {
            [RokiCommand, Usage, Description, Aliases]
            public async Task StockBuy(string symbol, Position position, long amount)
            {
                if (amount <= 0)
                {
                    await ctx.Channel.SendErrorAsync("Need to purchase at least 1 share").ConfigureAwait(false);
                    return;
                }

                var price = await _service.GetLatestPriceAsync(symbol).ConfigureAwait(false);
                if (price == null)
                {
                    await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                    return;
                }

                if (position == Position.Long)
                {
                    
                }
                else if (position == Position.Short)
                {
                    
                }
            }
            
            public enum Position
            {
                Long,
                Short
            }   
        }
    }
}