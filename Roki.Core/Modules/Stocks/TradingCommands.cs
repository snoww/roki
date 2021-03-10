using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Stocks.Extensions;
using Roki.Modules.Stocks.Services;
using Roki.Services;
using Roki.Services.Database.Models;

namespace Roki.Modules.Stocks
{
    public partial class Stocks
    {
        /*[Group]
        public class TradingCommands : RokiSubmodule<TradingService>
        {
            public enum Position
            {
                LONG,
                SHORT
            }

            private readonly IConfigurationService _config;

            public TradingCommands(IConfigurationService config)
            {
                _config = config;
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task StockSell(string symbol, long amount)
            {
                if (amount <= 0)
                {
                    await Context.Channel.SendErrorAsync("Need to sell at least 1 share").ConfigureAwait(false);
                    return;
                }

                symbol = symbol.ParseStockTicker();
                decimal? price = await Service.GetLatestPriceAsync(symbol).ConfigureAwait(false);
                if (price == null)
                {
                    await Context.Channel.SendErrorAsync("Unknown symbol").ConfigureAwait(false);
                    return;
                }

                Investment investment = await Service.GetUserInvestment(Context.User.Id, Context.Guild.Id, symbol);
                if (investment == null)
                {
                    await Context.Channel.SendErrorAsync($"You do not own any shares of `{symbol}`").ConfigureAwait(false);
                    return;
                }

                if (amount > investment.Shares)
                {
                    await Context.Channel.SendErrorAsync($"You only have `{investment.Shares}` of `{symbol}`").ConfigureAwait(false);
                    return;
                }

                Position pos = investment.Shares > 0 ? Position.LONG : Position.SHORT;
                if (pos == Position.LONG)
                {
                    Status status = await Service.LongPositionAsync(user, Context.Guild.Id.ToString(), symbol, "sell", price.Value, amount).ConfigureAwait(false);
                    if (status == Status.NotEnoughShares)
                    {
                        await Context.Channel.SendErrorAsync("You do not have enough in your Investing Account sell these shares").ConfigureAwait(false);
                        return;
                    }

                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription($"{Context.User.Mention}\nYou've successfully sold `{amount}` share{(amount == 1 ? string.Empty : "s")} of `{symbol}` at `{price.Value:N2}`\n" +
                                             $"Total Revenue: `{price.Value * amount:N2}`"))
                        .ConfigureAwait(false);
                }
                else
                {
                    TradingService.Status status = await Service.ShortPositionAsync(user, Context.Guild.Id.ToString(), symbol, "sell", price.Value, amount).ConfigureAwait(false);
                    if (status == TradingService.Status.NotEnoughInvesting)
                    {
                        await Context.Channel.SendErrorAsync("You do not have enough in your Investing Account sell these shares").ConfigureAwait(false);
                        return;
                    }

                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription($"{Context.User.Mention}\nYou've returned `{amount}` share{(amount == 1 ? string.Empty : "s")} of `{symbol}` back to the bank, at `{price.Value:N2}`\n" +
                                             $"Total Cost: `{price.Value * amount:N2}`"))
                        .ConfigureAwait(false);
                }
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task StockPosition(Position position, string symbol, long amount)
            {
                if (amount <= 0)
                {
                    await Context.Channel.SendErrorAsync("Need to purchase at least 1 share").ConfigureAwait(false);
                    return;
                }

                symbol = symbol.ParseStockTicker();
                decimal? price = await Service.GetLatestPriceAsync(symbol).ConfigureAwait(false);
                if (price == null)
                {
                    await Context.Channel.SendErrorAsync("Unknown symbol").ConfigureAwait(false);
                    return;
                }

                decimal cost = amount * price.Value;
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

                Investment investment = await Service.GetUserInvestment(Context.User.Id, Context.Guild.Id, symbol);
                if (position == Position.LONG)
                {
                    TradingService.Status status = await Service.LongPositionAsync(user, Context.Guild.Id.ToString(), symbol, "buy", price.Value, amount).ConfigureAwait(false);
                    if (status == TradingService.Status.NotEnoughInvesting)
                    {
                        await Context.Channel.SendErrorAsync("You do not have enough in your Investing Account to invest").ConfigureAwait(false);
                        return;
                    }

                    if (status == TradingService.Status.OwnsShortShares)
                    {
                        await Context.Channel.SendErrorAsync("You already own shorted shares of this company.").ConfigureAwait(false);
                        return;
                    }

                    EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context);
                    if (amount == 1)
                    {
                        embed.WithDescription($"{Context.User.Mention}\nYou've successfully purchased `1` share of `{symbol.ToUpper()}` at `{price.Value:N2}`\n" +
                                              $"Total Cost: `{cost:N2}` {guildConfig.CurrencyIcon}");
                    }
                    else
                    {
                        embed.WithDescription($"{Context.User.Mention}\nYou've successfully purchased `{amount}` shares of `{symbol.ToUpper()}` at `{price.Value:N2}`\n" +
                                              $"Total Cost: `{cost:N2}` {guildConfig.CurrencyIcon}");
                    }

                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
                else
                {
                    TradingService.Status status = await Service.ShortPositionAsync(user, Context.Guild.Id.ToString(), symbol, "buy", price.Value, amount).ConfigureAwait(false);
                    if (status == TradingService.Status.TooMuchLeverage)
                    {
                        await Context.Channel.SendErrorAsync($"You have leveraged over `{100000:N2}` {guildConfig.CurrencyIcon}.\n" +
                                                             "You cannot short any more stocks until they are returned.").ConfigureAwait(false);
                        return;
                    }

                    if (status == TradingService.Status.OwnsLongShares)
                    {
                        await Context.Channel.SendErrorAsync("You already own long shares of this company.").ConfigureAwait(false);
                        return;
                    }

                    EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context);
                    if (amount == 1)
                    {
                        embed.WithDescription($"{Context.User.Mention}\nYou've successfully sold `1` share of `{symbol.ToUpper()}` at `{price.Value}`\n" +
                                              $"Total sold for: `{cost:N2}` {guildConfig.CurrencyIcon}");
                    }
                    else
                    {
                        embed.WithDescription($"{Context.User.Mention}\nYou've successfully sold `{amount}` shares of `{symbol.ToUpper()}` at `{price.Value}`\n" +
                                              $"Total sold for: `{cost:N2}` {guildConfig.CurrencyIcon}");
                    }

                    embed.WithFooter("Short selling stocks charges a premium weekly");

                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
            }
        } */
    }
}