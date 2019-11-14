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
            var portfolios = await uow.DUsers.GetAllPortfolios().ConfigureAwait(false);
            foreach (var (userId, portfolio) in portfolios)
            {
                var interestList = portfolio.Where(investment => !investment.Position.Equals("long", StringComparison.OrdinalIgnoreCase))
                    .Where(investment => investment.InterestDate != null && !(DateTime.UtcNow.AddDays(1) < investment.InterestDate.Value))
                    .ToList();
                if (interestList.Count <= 0) continue;
                var user = _client.GetUser(userId);
                var dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                decimal total = 0;
                var embed = new EmbedBuilder().WithOkColor().WithTitle("Short Interest Charge");
                foreach (var investment in interestList)
                {
                    var cost = await CalculateInterest(investment.Symbol, investment.Shares).ConfigureAwait(false);
                    await uow.DUsers.ChargeInterestAsync(userId, cost).ConfigureAwait(false);
                    uow.Transaction.Add(new CurrencyTransaction
                    {
                       Amount = (long) cost,
                       Reason = "Short sell interest charge",
                       To = _roki.Properties.BotId.ToString(),
                       From = userId.ToString(),
                    });
                    total += cost;
                    embed.AddField($"{investment.Symbol} - {investment.Shares} Shares", $"{cost} {_roki.Properties.CurrencyIcon}", true);
                }

                embed.WithDescription($"You have been charged a total of {total} {_roki.Properties.CurrencyIcon}\n");
                await dm.EmbedAsync(embed).ConfigureAwait(false);
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<decimal?> GetLatestPriceAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/quote/latestPrice?token={_config.IexToken}").ConfigureAwait(false);
            var success = decimal.TryParse(result, out var price);
            if (!success) return null;
            return price;
        }

        public async Task<bool> UpdateInvAccountAsync(ulong userId, decimal amount)
        {
            using var uow = _db.GetDbContext();
            var success = await uow.DUsers.UpdateInvestingAccountAsync(userId, amount).ConfigureAwait(false);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            return success;
        }

        public async Task<List<Investment>> GetUserPortfolioAsync(ulong userId)
        {
            using var uow = _db.GetDbContext();
            return await uow.DUsers.GetOrCreateUserPortfolio(userId).ConfigureAwait(false);
        }

        public async Task<bool> UpdateUserPortfolioAsync(ulong userId, string symbol, Stocks.TradingCommands.Position position, 
            string action, decimal price, long amount)
        {
            using var uow = _db.GetDbContext();
            var portfolio = await uow.DUsers.GetOrCreateUserPortfolio(userId).ConfigureAwait(false);
            if (!portfolio.Any(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)) && price * amount >= 100000)
                return false;
            if (await CanShortStock(portfolio).ConfigureAwait(false))
                return false;
            
            var pos = position == Stocks.TradingCommands.Position.Long ? "long" : "short";
            uow.Trades.Add(new Trades
            {
                UserId = userId,
                Symbol = symbol.ToUpper(),
                Position = pos,
                Action = action,
                Shares = amount,
                Price = price,
                TransactionDate = DateTime.UtcNow
            });
            var success = await uow.DUsers.UpdateUserPortfolio(userId, symbol, pos, amount);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            return success;
        }
        
        private async Task<bool> CanShortStock(List<Investment> portfolio)
        {
            var value = 0m;
            foreach (var investment in portfolio.Where(investment => investment.Position.Equals("short", StringComparison.OrdinalIgnoreCase)))
            {
                var price = await GetLatestPriceAsync(investment.Symbol).ConfigureAwait(false);
                value += price.Value * investment.Shares;
            }
            return value <= 100000;
        }

        private async Task<decimal> CalculateInterest(string symbol, long shares)
        {
            var price = (await GetLatestPriceAsync(symbol).ConfigureAwait(false)).Value;
            return price * shares * 0.025m;
        }
    }
}