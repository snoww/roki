using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Stocks.Services
{
    public class PortfolioService : IRokiService
    {
        private readonly IRokiConfig _config;
        private readonly IMongoService _mongo;
        private readonly IHttpClientFactory _http;
        private const string IexStocksUrl = "https://cloud.iexapis.com/stable/stock/";

        public PortfolioService(IHttpClientFactory http, IRokiConfig config, IMongoService mongo)
        {
            _http = http;
            _config = config;
            _mongo = mongo;
        }

        public async Task<List<Investment>> GetUserPortfolio(ulong userId)
        {
            return (await _mongo.Context.GetUserAsync(userId).ConfigureAwait(false)).Portfolio;
        }

        public async Task<decimal> GetPortfolioValue(IEnumerable<Investment> portfolio)
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
            using var http = _http.CreateClient();
            var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/quote/latestPrice?token={_config.IexToken}").ConfigureAwait(false);
            decimal.TryParse(result, out var price);
            return price;
        }
    }
}