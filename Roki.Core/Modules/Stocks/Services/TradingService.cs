using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;

namespace Roki.Modules.Stocks.Services
{
    public class TradingService : IRService
    {
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private readonly Roki _roki;
        private Timer _timer;
        private const string IexStocksUrl = "https://cloud.iexapis.com/stable/stock/";

        public TradingService(IHttpClientFactory httpFactory, IRokiConfig config, DbService db, DiscordSocketClient client, Roki roki)
        {
            _httpFactory = httpFactory;
            _config = config;
            _db = db;
            _client = client;
            _roki = roki;
            ShortPremiumTimer();
        }

        private void ShortPremiumTimer()
        {
            _timer = new Timer(ChargePremium, null, TimeSpan.Zero, TimeSpan.FromHours(3));
        }

        private async void ChargePremium(object state)
        {
            using var uow = _db.GetDbContext();
            var portfolios = await uow.Users.GetAllPortfolios().ConfigureAwait(false);
            foreach (var (userId, portfolio) in portfolios)
            {
                var interestList = portfolio.Where(investment => !investment.Position.Equals("long", StringComparison.OrdinalIgnoreCase))
                    .Where(investment => investment.InterestDate != null && DateTimeOffset.UtcNow.AddDays(1) >= investment.InterestDate.Value)
                    .ToList();
                if (interestList.Count <= 0) continue;
                var user = _client.GetUser(userId);
                var dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                decimal total = 0;
                var embed = new EmbedBuilder().WithOkColor().WithTitle("Short Interest Charge");
                foreach (var investment in interestList)
                {
                    var cost = await CalculateInterest(investment.Symbol, investment.Shares).ConfigureAwait(false);
                    await uow.Users.ChargeInterestAsync(userId, cost).ConfigureAwait(false);
                    investment.InterestDate = DateTimeOffset.UtcNow + TimeSpan.FromDays(7);
                    uow.Transaction.Add(new CurrencyTransaction
                    {
                       Amount = (long) cost,
                       Reason = "Short position interest charge",
                       To = _roki.Properties.BotId,
                       From = userId
                    });
                    total += cost;
                    embed.AddField($"`{investment.Symbol}` - `{investment.Shares}` Shares", $"`{cost}` {_roki.Properties.CurrencyIcon}", true);
                }

                embed.WithDescription($"You have been charged a total of `{total}` {_roki.Properties.CurrencyIcon}\n");
                await dm.EmbedAsync(embed).ConfigureAwait(false);
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<decimal?> GetLatestPriceAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
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

        public async Task<Investment> GetOwnedShares(ulong userId, string symbol)
        {
            using var uow = _db.GetDbContext();
            var portfolio = await uow.Users.GetUserPortfolioAsync(userId).ConfigureAwait(false);
            var inv = portfolio.FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            return inv;
        }

        public async Task<Status> ShortPositionAsync(ulong userId, string symbol, string action, decimal price, long amount)
        {
            using var uow = _db.GetDbContext();
            var existing = (await uow.Users.GetUserPortfolioAsync(userId).ConfigureAwait(false)).FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (existing != null && !existing.Position.Equals("short"))
            {
                return Status.OwnsLongShares;
            }
            var total = price * amount;
            bool success;
            var portfolio = await uow.Users.GetUserPortfolioAsync(userId).ConfigureAwait(false);
            if (action == "buy") // LENDING SHARES FROM BANK, SELLING IMMEDIATELY
            {
                if (!portfolio.Any(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)) && total >= 100000)
                    return Status.TooMuchLeverage;
                if (!await CanShortStock(portfolio, total).ConfigureAwait(false))
                    return Status.TooMuchLeverage;
                await uow.Users.UpdateInvestingAccountAsync(userId, total).ConfigureAwait(false);
                success = await uow.Users.UpdateUserPortfolio(userId, symbol, "short", action, amount);
            }
            else // SELLING SHARES BACK TO BANK
            {
                var update = await uow.Users.UpdateInvestingAccountAsync(userId, -total).ConfigureAwait(false);
                if (!update) return Status.NotEnoughInvesting;
                success = await uow.Users.UpdateUserPortfolio(userId, symbol, "short", action, -amount);
            }

            if (!success) return Status.NotEnoughShares;
            
            uow.Trades.Add(new Trades
            {
                UserId = userId,
                Symbol = symbol,
                Position = "short",
                Action = action,
                Shares = amount,
                Price = price,
                TransactionDate = DateTimeOffset.UtcNow
            });
            
            await uow.SaveChangesAsync().ConfigureAwait(false);
            return Status.Success;
        }
        
        public async Task<Status> LongPositionAsync(ulong userId, string symbol, string action, decimal price, long amount)
        {
            using var uow = _db.GetDbContext();
            var existing = (await uow.Users.GetUserPortfolioAsync(userId).ConfigureAwait(false)).FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (existing != null && !existing.Position.Equals("long"))
            {
                return Status.OwnsShortShares;
            }
            var total = price * amount;
            bool success;
            if (action == "buy") // BUYING SHARES
            {
                var update = await uow.Users.UpdateInvestingAccountAsync(userId, -total).ConfigureAwait(false);
                if (!update) return Status.NotEnoughInvesting;
                success = await uow.Users.UpdateUserPortfolio(userId, symbol, "long", action, amount);
            }
            else // SELLING SHARES
            {
                await uow.Users.UpdateInvestingAccountAsync(userId, total).ConfigureAwait(false);
                success = await uow.Users.UpdateUserPortfolio(userId, symbol, "long", action, -amount);
            }

            if (!success) return Status.NotEnoughShares;
            
            uow.Trades.Add(new Trades
            {
                UserId = userId,
                Symbol = symbol,
                Position = "short",
                Action = action,
                Shares = amount,
                Price = price,
                TransactionDate = DateTimeOffset.UtcNow
            });
            
            await uow.SaveChangesAsync().ConfigureAwait(false);
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