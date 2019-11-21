using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;

namespace Roki.Modules.Stocks.Services
{
    public class PortfolioService : IRService
    {
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly DbService _db;
        private const string IexStocksUrl = "https://cloud.iexapis.com/stable/stock/";

        public PortfolioService(DbService db, IHttpClientFactory httpFactory, IRokiConfig config)
        {
            _db = db;
            _httpFactory = httpFactory;
            _config = config;
        }

        public async Task<List<Investment>> GetUserPortfolio(ulong userId)
        {
            using var uow = _db.GetDbContext();
            return await uow.DUsers.GetUserPortfolio(userId).ConfigureAwait(false);
        }

        public async Task<decimal> GetPortfolioValue(List<Investment> portfolio)
        {
            var value = 0m;
            foreach (var investment in portfolio)
            {
                var price = await GetStockPrice(investment.Symbol).ConfigureAwait(false);
                value += price * investment.Shares;
            }

            return value;
        }

        private async Task<decimal> GetStockPrice(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/quote/latestPrice?token={_config.IexToken}").ConfigureAwait(false);
            decimal.TryParse(result, out var price);
            return price;
        }
    }
}