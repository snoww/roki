using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Stocks.Extensions;
using Roki.Modules.Stocks.Services;

namespace Roki.Modules.Stocks
{
    public partial class Stocks
    {
        [Group]
        public class TradingCommands : RokiSubmodule<TradingService>
        {
            private readonly Roki _roki;

            public TradingCommands(Roki roki)
            {
                _roki = roki;
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task StockBuy(string symbol, long amount)
            {
                if (amount <= 0)
                {
                    await ctx.Channel.SendErrorAsync("Need to purchase at least 1 share").ConfigureAwait(false);
                    return;
                }

                var price = await _service.GetLatestPriceAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
                if (price == null)
                {
                    await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                    return;
                }
                
                var cost = amount * price.Value;
                var removed = await _service.UpdateInvAccountAsync(ctx.User.Id, -cost).ConfigureAwait(false);
                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync("You do not have enough in your Investing Account to invest").ConfigureAwait(false);
                    return;
                }

                await _service.UpdateUserPortfolioAsync(ctx.User.Id, symbol, Position.Long, "buy", price.Value, amount).ConfigureAwait(false);
                var embed = new EmbedBuilder().WithOkColor();
                if (amount == 1)
                    embed.WithDescription($"{ctx.User.Mention}\nYou've successfully purchased `1` share of `{symbol.ToUpper()}` at `{price.Value:N2}`\n" +
                                          $"Total Cost: `{cost:N2}` {_roki.Properties.CurrencyIcon}");
                else
                    embed.WithDescription($"{ctx.User.Mention}\nYou've successfully purchased `{amount}` shares of `{symbol.ToUpper()}` at `{price.Value:N2}`\n" +
                                          $"Total Cost: `{cost:N2}` {_roki.Properties.CurrencyIcon}");

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task StockSell(string symbol, long amount)
            {
                if (amount <= 0)
                {
                    await ctx.Channel.SendErrorAsync("Need to sell at least 1 share").ConfigureAwait(false);
                    return;
                }

                var price = await _service.GetLatestPriceAsync(symbol.ParseStockTicker()).ConfigureAwait(false);
                if (price == null)
                {
                    await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                    return;
                }

                var cost = amount * price.Value;
                var success = await _service.UpdateUserPortfolioAsync(ctx.User.Id, symbol, Position.Short, "sell", price.Value, -amount).ConfigureAwait(false);
                await _service.UpdateInvAccountAsync(ctx.User.Id, cost).ConfigureAwait(false);
                if (!success)
                {
                    await ctx.Channel.SendErrorAsync($"You do not have that many shares to sell, or you cannot have more than `100,000` in liabilities.").ConfigureAwait(false);
                    return;
                }
                var embed = new EmbedBuilder().WithOkColor();
                if (amount == 1)
                    embed.WithDescription($"{ctx.User.Mention}\nYou've successfully sold `1` share of `{symbol.ToUpper()}` at `{price.Value}`\n" +
                                          $"Total sold for: `{cost:N2}` {_roki.Properties.CurrencyIcon}");
                else
                    embed.WithDescription($"{ctx.User.Mention}\nYou've successfully sold `{amount}` shares of `{symbol.ToUpper()}` at `{price.Value}`\n" +
                                          $"Total sold for: `{cost:N2}` {_roki.Properties.CurrencyIcon}");
                embed.WithFooter("Short selling stocks charges a premium weekly");

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            
            public enum Position
            {
                Long,
                Short
            }
        }
    }
}