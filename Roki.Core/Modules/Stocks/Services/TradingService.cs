using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NLog;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;

namespace Roki.Modules.Stocks.Services
{
    /*public class TradingService : IRokiService
    {
        private const string IexStocksUrl = "https://cloud.iexapis.com/stable/stock/";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DiscordSocketClient _client;
        private readonly IRokiDbService _dbService;
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _factory;
        // private Timer _timer;

        public TradingService(IHttpClientFactory factory, IRokiConfig config, DiscordSocketClient client, IRokiDbService dbService)
        {
            _factory = factory;
            _config = config;
            _client = client;
            _dbService = dbService;
            // ShortPremiumTimer();
        }

        private void ShortPremiumTimer()
        {
            // _timer = new Timer(ChargePremium, null, TimeSpan.Zero, TimeSpan.FromHours(3));
        }

        /*
        private async void ChargePremium(object state)
        {
            using var cursor = await _mongo.Context.GetAllUserCursorAsync().ConfigureAwait(false);
            while (await cursor.MoveNextAsync())
            {
                foreach (var dbUser in cursor.Current)
                {
                    var interestList = dbUser.Portfolio.Where(x => !x.Position.Equals("long", StringComparison.OrdinalIgnoreCase))
                        .Where(x => x.InterestDate != null && DateTime.UtcNow.AddDays(1).Date >= x.InterestDate.Value)
                        .ToList();
                    if (interestList.Count <= 0) 
                        continue;
                    
                    var user = _client.GetUser(dbUser.Id);
                    var dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                    decimal total = 0;
                    var embed = new EmbedBuilder().WithOkColor().WithTitle("Short Interest Charge");
                    foreach (var investment in interestList)
                    {
                        var cost = await CalculateInterest(investment.ticker, investment.Shares).ConfigureAwait(false);
                        await _mongo.Context.ChargeInterestAsync(dbUser, TODO, cost, investment.ticker);
                        await _mongo.Context.AddTransaction(new Transaction
                        {
                           Amount = (long) cost,
                           Reason = "Short position interest charge",
                           To = Roki.Properties.BotId,
                           From = user.Id
                        });
                        
                        total += cost;
                        embed.AddField($"`{investment.ticker}` - `{investment.Shares}` Shares", $"`{cost:C2}` {Roki.Properties.CurrencyIcon}", true);
                    }
                    
                    embed.WithDescription($"You have been charged a total of `{total}` {Roki.Properties.CurrencyIcon}\n");
                    
                    try
                    {
                        await dm.EmbedAsync(embed).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn(e, "Unable to send DM to {user}", user);
                    }
                }
            }
        }
        #1#

        public async Task<decimal?> GetLatestPriceAsync(string symbol)
        {
            using HttpClient http = _factory.CreateClient();
            try
            {
                string result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/quote/latestPrice?token={_config.IexToken}").ConfigureAwait(false);
                return decimal.Parse(result);
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public async Task<Investment> GetUserInvestment(ulong userId, ulong guildId, string symbol)
        {
            return await _dbService.Context.Investments.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.GuildId == guildId && x.Symbol == symbol);
        }

        public async Task<Status> ShortPositionAsync(User user, string guildId, string symbol, string action, decimal price, long amount)
        {
            Dictionary<string, Investment> portfolio = user.Data[guildId].Portfolio;

            Investment existing = portfolio.ContainsKey(symbol) ? user.Data[guildId].Portfolio[symbol] : null;
            if (existing != null && !existing.Position.Equals("short"))
            {
                return Status.OwnsLongShares;
            }

            decimal total = price * amount;
            bool success;
            if (action == "buy") // LENDING SHARES FROM BANK, SELLING IMMEDIATELY
            {
                if (!portfolio.ContainsKey(symbol) && total >= 100000)
                {
                    return Status.TooMuchLeverage;
                }

                if (!await CanShortStock(portfolio, total).ConfigureAwait(false))
                {
                    return Status.TooMuchLeverage;
                }

                await _mongo.Context.UpdateUserInvestingAccountAsync(user, guildId, total).ConfigureAwait(false);
                success = await _mongo.Context.UpdateUserPortfolioAsync(user, guildId, symbol, "short", amount).ConfigureAwait(false);
            }
            else // SELLING SHARES BACK TO BANK
            {
                bool update = await _mongo.Context.UpdateUserInvestingAccountAsync(user, guildId, -total).ConfigureAwait(false);
                if (!update) return Status.NotEnoughInvesting;
                success = await _mongo.Context.UpdateUserPortfolioAsync(user, guildId, symbol, "short", -amount).ConfigureAwait(false);
            }

            if (!success) return Status.NotEnoughShares;

            await _mongo.Context.AddTrade(new Trade
            {
                UserId = user.Id,
                Ticker = symbol,
                Position = "short",
                Action = action,
                Shares = amount,
                Price = price
            }).ConfigureAwait(false);

            return Status.Success;
        }

        public async Task<Status> LongPositionAsync(User user, string guildId, string symbol, string action, decimal price, long amount)
        {
            Investment existing = user.Data[guildId].Portfolio.ContainsKey(symbol) ? user.Data[guildId].Portfolio[symbol] : null;
            if (existing != null && !existing.Position.Equals("long"))
            {
                return Status.OwnsLongShares;
            }

            decimal total = price * amount;
            bool success;
            if (action == "buy") // BUYING SHARES
            {
                bool update = await _mongo.Context.UpdateUserInvestingAccountAsync(user, guildId, -total).ConfigureAwait(false);
                if (!update) return Status.NotEnoughInvesting;
                success = await _mongo.Context.UpdateUserPortfolioAsync(user, guildId, symbol, "long", amount).ConfigureAwait(false);
            }
            else // SELLING SHARES
            {
                await _mongo.Context.UpdateUserInvestingAccountAsync(user, guildId, total).ConfigureAwait(false);
                success = await _mongo.Context.UpdateUserPortfolioAsync(user, guildId, symbol, "long", -amount).ConfigureAwait(false);
            }

            if (!success) return Status.NotEnoughShares;

            await _mongo.Context.AddTrade(new Trade
            {
                UserId = user.Id,
                Ticker = symbol,
                Position = "long",
                Action = action,
                Shares = amount,
                Price = price
            });

            return Status.Success;
        }

        private async Task<bool> CanShortStock(Dictionary<string, Investment> portfolio, decimal cost)
        {
            // foreach (Investment investment in portfolio.Where(investment => investment.Position.Equals("short", StringComparison.OrdinalIgnoreCase)))
            // {
            //     decimal? price = await GetLatestPriceAsync(investment.ticker).ConfigureAwait(false);
            //     cost += price.Value * investment.Shares;
            // }

            foreach ((string ticker, Investment investment) in portfolio)
            {
                if (investment.Position.Equals("short", StringComparison.Ordinal))
                {
                    decimal? price = await GetLatestPriceAsync(ticker).ConfigureAwait(false);
                    cost += (price ?? 0) * investment.Shares;
                }
            }

            return cost <= 100000;
        }

        private async Task<decimal> CalculateInterest(string symbol, long shares)
        {
            decimal? price = await GetLatestPriceAsync(symbol).ConfigureAwait(false);
            if (price.HasValue)
            {
                return price.Value * shares * 0.025m;
            }

            return 0;
        }
    }*/
    
    public enum Status
    {
        NotEnoughInvesting = -1,
        NotEnoughShares = -2,
        TooMuchLeverage = -3,
        OwnsShortShares = -4,
        OwnsLongShares = -5,
        Success = 1
    }
}