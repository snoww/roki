using System;
using System.Threading.Tasks;
using Discord;
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
            private readonly Roki _roki;

            public TradingCommands(Roki roki)
            {
                _roki = roki;
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task StockBuy(string symbol, long amount, Position position = Position.Long)
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

                bool removed;
                var embed = new EmbedBuilder().WithOkColor();
                if (position == Position.Short)
                {
                    var fee = amount * price.Value * 0.05m;
                    removed = await _service.UpdateInvAccountAsync(ctx.User.Id, -fee).ConfigureAwait(false);
                    if (!removed)
                    {
                        await ctx.Channel.SendErrorAsync("You do not have enough in your Investing Account to invest").ConfigureAwait(false);
                        return;
                    }

                    await _service.UpdateUserPortfolioAsync(ctx.User.Id, symbol, Position.Short, "buy", price.Value, amount).ConfigureAwait(false);
                    if (amount == 1)
                        embed.WithDescription($"{ctx.User.Mention}\nYou have a loan of `1` share of `{symbol.ToUpper()}` at `{price.Value}`\n" +
                                              $"Value: `{price.Value}` {_roki.Properties.CurrencyIcon}");
                    else
                        embed.WithDescription($"{ctx.User.Mention}\nYou have a loan of `{amount}` share of `{symbol.ToUpper()}` at `{price.Value}`\n" +
                                              $"Value: `{amount * price.Value}` {_roki.Properties.CurrencyIcon}");
                    embed.WithFooter(Format.Italics("Expires in 7 days unless renewed."));

                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    return;
                }

                var cost = amount * price.Value;
                removed = await _service.UpdateInvAccountAsync(ctx.User.Id, -cost).ConfigureAwait(false);
                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync("You do not have enough in your Investing Account to invest").ConfigureAwait(false);
                    return;
                }

                await _service.UpdateUserPortfolioAsync(ctx.User.Id, symbol, Position.Long, "buy", price.Value, amount).ConfigureAwait(false);
                if (amount == 1)
                    embed.WithDescription($"{ctx.User.Mention}\nYou've successfully purchased `1` share of `{symbol.ToUpper()}` at `{price.Value}`\n" +
                                          $"Total Cost: `{cost}` {_roki.Properties.CurrencyIcon}");
                else
                    embed.WithDescription($"{ctx.User.Mention}\nYou've successfully purchased `{amount}` shares of `{symbol.ToUpper()}` at `{price.Value}`\n" +
                                          $"Total Cost: `{cost}` {_roki.Properties.CurrencyIcon}");

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

                var cost = (long) Math.Ceiling(amount * price.Value);
                await _service.UpdateInvAccountAsync(ctx.User.Id, cost).ConfigureAwait(false);
                var success = await _service.UpdateUserPortfolioAsync(ctx.User.Id, symbol, Position.Long, "sell", price.Value, -amount).ConfigureAwait(false);
                if (!success)
                {
                    await ctx.Channel.SendErrorAsync($"You do not have that many shares to sell.").ConfigureAwait(false);
                    return;
                }
                var embed = new EmbedBuilder().WithOkColor();
                if (amount == 1)
                    embed.WithDescription($"{ctx.User.Mention}\nYou've successfully sold `1` share of `{symbol.ToUpper()}` at `{price.Value}`\n" +
                                          $"Total sold for: `{cost}` {_roki.Properties.CurrencyIcon}");
                else
                    embed.WithDescription($"{ctx.User.Mention}\nYou've successfully sold `{amount}` shares of `{symbol.ToUpper()}` at `{price.Value}`\n" +
                                          $"Total sold for: `{cost}` {_roki.Properties.CurrencyIcon}");

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