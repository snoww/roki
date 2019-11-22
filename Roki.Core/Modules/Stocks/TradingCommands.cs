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
            public async Task StockSell(string symbol, long amount)
            {
                if (amount <= 0)
                {
                    await ctx.Channel.SendErrorAsync("Need to sell at least 1 share").ConfigureAwait(false);
                    return;
                }

                symbol = symbol.ParseStockTicker();
                var price = await _service.GetLatestPriceAsync(symbol).ConfigureAwait(false);
                if (price == null)
                {
                    await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                    return;
                }

                var investment = await _service.GetOwnedShares(ctx.User.Id, symbol).ConfigureAwait(false);
                if (investment == null)
                {
                    await ctx.Channel.SendErrorAsync($"You do not own any shares of `{symbol}`").ConfigureAwait(false);
                    return;
                }

                if (amount > investment.Shares)
                {
                    await ctx.Channel.SendErrorAsync($"You only have `{investment.Shares}` of `{symbol}`").ConfigureAwait(false);
                    return;
                }

                Enum.TryParse<Position>(investment.Position, out var pos);
                if (pos == Position.Long)
                {
                    await _service.LongPositionAsync(ctx.User.Id, symbol, "sell", price.Value, amount).ConfigureAwait(false);
                }
                else if (pos == Position.Short)
                {
                    var status = await _service.ShortPositionAsync(ctx.User.Id, symbol, "sell", price.Value, amount).ConfigureAwait(false);
                    if (status == TradingService.Status.NotEnoughInvesting)
                    {
                        await ctx.Channel.SendErrorAsync("You do not have enough in your Investing Account sell these shares").ConfigureAwait(false);
                    }
                }
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task StockPosition(Position position, string symbol, long amount)
            {
                if (amount <= 0)
                {
                    await ctx.Channel.SendErrorAsync("Need to purchase at least 1 share").ConfigureAwait(false);
                    return;
                }

                symbol = symbol.ParseStockTicker();
                var price = await _service.GetLatestPriceAsync(symbol).ConfigureAwait(false);
                if (price == null)
                {
                    await ctx.Channel.SendErrorAsync("Unknown Symbol").ConfigureAwait(false);
                    return;
                }
                var cost = amount * price.Value;
                if (position == Position.Long)
                {
                    var status = await _service.LongPositionAsync(ctx.User.Id, symbol, "buy", price.Value, amount).ConfigureAwait(false);
                    if (status == TradingService.Status.NotEnoughInvesting)
                    {
                        await ctx.Channel.SendErrorAsync("You do not have enough in your Investing Account to invest").ConfigureAwait(false);
                        return;
                    }
                    var embed = new EmbedBuilder().WithOkColor();
                    if (amount == 1)
                        embed.WithDescription($"{ctx.User.Mention}\nYou've successfully purchased `1` share of `{symbol.ToUpper()}` at `{price.Value:N2}`\n" +
                                              $"Total Cost: `{cost:N2}` {_roki.Properties.CurrencyIcon}");
                    else
                        embed.WithDescription($"{ctx.User.Mention}\nYou've successfully purchased `{amount}` shares of `{symbol.ToUpper()}` at `{price.Value:N2}`\n" +
                                              $"Total Cost: `{cost:N2}` {_roki.Properties.CurrencyIcon}");

                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
                else
                {
                    var status = await _service.ShortPositionAsync(ctx.User.Id, symbol, "buy", price.Value, amount).ConfigureAwait(false);
                    if (status == TradingService.Status.TooMuchLeverage)
                    {
                        await ctx.Channel.SendErrorAsync($"You have leveraged over `{100000:N2}` {_roki.Properties.CurrencyIcon}.\n" +
                                                         "You cannot short any more stocks until they are returned.").ConfigureAwait(false);
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
            }
            
            public enum Position
            {
                Long,
                Short
            }
        }
    }
}