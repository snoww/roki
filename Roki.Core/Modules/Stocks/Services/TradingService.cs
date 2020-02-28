using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NLog;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Stocks.Services
{
    public class TradingService : IRokiService
    {
        private readonly IRokiConfig _config;
        private readonly IMongoService _mongo;
        private readonly IHttpClientFactory _http;
        private readonly DiscordSocketClient _client;
        private Timer _timer;
        private const string IexStocksUrl = "https://cloud.iexapis.com/stable/stock/";

        private static Logger Logger = LogManager.GetCurrentClassLogger();

        public TradingService(IHttpClientFactory http, IRokiConfig config, DiscordSocketClient client, IMongoService mongo)
        {
            _http = http;
            _config = config;
            _client = client;
            _mongo = mongo;
            ShortPremiumTimer();
        }

        private void ShortPremiumTimer()
        {
            _timer = new Timer(ChargePremium, null, TimeSpan.Zero, TimeSpan.FromHours(3));
        }

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
                        var cost = await CalculateInterest(investment.Symbol, investment.Shares).ConfigureAwait(false);
                        await _mongo.Context.ChargeInterestAsync(dbUser, cost, investment.Symbol);
                        await _mongo.Context.AddTransaction(new Transaction
                        {
                           Amount = (long) cost,
                           Reason = "Short position interest charge",
                           To = Roki.Properties.BotId,
                           From = user.Id
                        });
                        
                        total += cost;
                        embed.AddField($"`{investment.Symbol}` - `{investment.Shares}` Shares", $"`{cost:C2}` {Roki.Properties.CurrencyIcon}", true);
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

        public async Task<decimal?> GetLatestPriceAsync(string symbol)
        {
            using var http = _http.CreateClient();
            try
            {
                var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/quote/latestPrice?token={_config.IexToken}").ConfigureAwait(false);
                decimal.TryParse(result, out var price);
                return price;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public async Task<Status> ShortPositionAsync(User user, string symbol, string action, decimal price, long amount)
        {
            var existing = user.Portfolio.FirstOrDefault(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (existing != null && !existing.Position.Equals("short"))
            {
                return Status.OwnsLongShares;
            }
            var total = price * amount;
            bool success;
            var portfolio = user.Portfolio;
            if (action == "buy") // LENDING SHARES FROM BANK, SELLING IMMEDIATELY
            {
                if (!portfolio.Any(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)) && total >= 100000)
                    return Status.TooMuchLeverage;
                if (!await CanShortStock(portfolio, total).ConfigureAwait(false))
                    return Status.TooMuchLeverage;
                await _mongo.Context.UpdateUserInvestingAccountAsync(user, total).ConfigureAwait(false);
                success = await _mongo.Context.UpdateUserPortfolioAsync(user, symbol, "short", action, amount).ConfigureAwait(false);
            }
            else // SELLING SHARES BACK TO BANK
            {
                var update = await _mongo.Context.UpdateUserInvestingAccountAsync(user, -total).ConfigureAwait(false);
                if (!update) return Status.NotEnoughInvesting;
                success = await _mongo.Context.UpdateUserPortfolioAsync(user, symbol, "short", action, -amount).ConfigureAwait(false);
            }

            if (!success) return Status.NotEnoughShares;
            
            await _mongo.Context.AddTrade(new Trade
            {
                UserId = user.Id,
                Symbol = symbol,
                Position = "short",
                Action = action,
                Shares = amount,
                Price = price,
            }).ConfigureAwait(false);
            
            return Status.Success;
        }
        
        public async Task<Status> LongPositionAsync(User user, string symbol, string action, decimal price, long amount)
        {
            var existing = user.Portfolio.FirstOrDefault(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (existing != null && !existing.Position.Equals("long"))
            {
                return Status.OwnsShortShares;
            }
            var total = price * amount;
            bool success;
            if (action == "buy") // BUYING SHARES
            {
                var update = await _mongo.Context.UpdateUserInvestingAccountAsync(user, -total).ConfigureAwait(false);
                if (!update) return Status.NotEnoughInvesting;
                success = await _mongo.Context.UpdateUserPortfolioAsync(user, symbol, "long", action, amount).ConfigureAwait(false);
            }
            else // SELLING SHARES
            {
                await _mongo.Context.UpdateUserInvestingAccountAsync(user, total).ConfigureAwait(false);
                success = await _mongo.Context.UpdateUserPortfolioAsync(user, symbol, "long", action, -amount).ConfigureAwait(false);
            }

            if (!success) return Status.NotEnoughShares;
            
            await _mongo.Context.AddTrade(new Trade
            {
                UserId = user.Id,
                Symbol = symbol,
                Position = "short",
                Action = action,
                Shares = amount,
                Price = price,
            });
            
            return Status.Success;
        }
        
        private async Task<bool> CanShortStock(List<Investment> portfolio, decimal cost)
        {
            foreach (var investment in portfolio.Where(investment => investment.Position.Equals("short", StringComparison.OrdinalIgnoreCase)))
            {
                var price = await GetLatestPriceAsync(investment.Symbol).ConfigureAwait(false);
                cost += price.Value * investment.Shares;
            }
            return cost <= 100000;
        }

        private async Task<decimal> CalculateInterest(string symbol, long shares)
        {
            var price = (await GetLatestPriceAsync(symbol).ConfigureAwait(false)).Value;
            return price * shares * 0.025m;
        }
        
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
}