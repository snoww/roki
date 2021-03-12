using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
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
        [Group]
        [RequireContext(ContextType.Guild)]
        public class TradingCommands : RokiSubmodule<TradingService>
        {
            private readonly IConfigurationService _config;

            public TradingCommands(IConfigurationService config)
            {
                _config = config;
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task StockSell(long shares, string symbol)
            {
                if (shares <= 0)
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
                
                // doesn't hold any shares - wants to short
                // or
                // currently holding short - want to short more shares
                if (investment is not {Shares: > 0})
                {
                    // short selling not implemented
                    return;
                    // todo check if they have enough collateral and margin
                    // await Service.SellShares(Context.User.Id, Context.Guild.Id, symbol, shares, price.Value);
                    // return;
                }

                // currently holding long - want to sell shares
                if (investment.Shares > 0)
                {
                    if (investment.Shares < shares)
                    {
                        await Context.Channel.SendErrorAsync($"You only own `{investment.Shares}` shares of `{symbol}`").ConfigureAwait(false);
                        return;
                    }
                    await Service.SellShares(Context.User.Id, Context.Guild.Id, symbol, shares, price.Value);
                }
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task StockBuy(long shares, string symbol)
            {
                if (shares <= 0)
                {
                    await Context.Channel.SendErrorAsync("Need to buy at least 1 share").ConfigureAwait(false);
                    return;
                }

                symbol = symbol.ParseStockTicker();
                decimal? price = await Service.GetLatestPriceAsync(symbol).ConfigureAwait(false);
                if (price == null)
                {
                    await Context.Channel.SendErrorAsync("Unknown symbol").ConfigureAwait(false);
                    return;
                }

                decimal cost = shares * price.Value;
                decimal investing = await Service.GetInvestingAccount(Context.User.Id, Context.Guild.Id);
                if (investing < cost)
                {
                    await Context.Channel.SendErrorAsync("You do not have enough in your investing account to make that trade.\n" +
                                                         $"You can transfer from your cash account `{_config.GetGuildPrefix(Context.Guild.Id)}transfer to investing {Math.Ceiling(cost - investing):N0}`")
                        .ConfigureAwait(false);
                    return;
                }

                Investment investment = await Service.GetUserInvestment(Context.User.Id, Context.Guild.Id, symbol);
                
                // doesn't hold any shares - wants to buy
                // or
                // currently holding long - want to buy more shares
                if (investment is not {Shares: < 0})
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle("Confirm order?")
                        .WithDescription($"`{shares:N0}x` share(s) of `{symbol}`\nTotal cost: `{cost:N2}` {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon}")
                        .WithFooter("yes/no"));

                    IMessage reply = await GetUserReply(TimeSpan.FromSeconds(30));
                    var tries = 0;
                    while (true)
                    {
                        if (reply == null)
                        {
                            await Context.Channel.SendErrorAsync("Buy order cancelled, no confirmation received.");
                            return;
                        }

                        if (++tries > 2 || reply.Content.Equals("no", StringComparison.OrdinalIgnoreCase) && !reply.Content.Equals("n", StringComparison.OrdinalIgnoreCase))
                        {
                            await Context.Channel.SendErrorAsync("Buy order cancelled.");
                            return;
                        }
                        
                        if (reply.Content.Equals("yes") || !reply.Content.Equals("y"))
                        {
                            break;
                        }
                        
                        reply = await GetUserReply(TimeSpan.FromSeconds(30));
                    }

                    await Service.BuyShares(Context.User.Id, Context.Guild.Id, symbol, shares, price.Value);
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle("Order successful")
                        .WithDescription($"`{shares:N0}x {symbol}` for `{cost:N2}`"));
                    return;
                }
                
                // currently shorting, wants to buy back
                // todo margin calculation
                await Service.BuyShares(Context.User.Id, Context.Guild.Id, symbol, shares, price.Value);
            }
        } 
    }
}