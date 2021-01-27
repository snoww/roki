using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Stocks.Services
{
    public class PortfolioService : IRokiService
    {
        private readonly IRokiConfig _config;
        private readonly IMongoService _mongo;
        private readonly IHttpClientFactory _factory;
        private const string IexStocksUrl = "https://cloud.iexapis.com/stable/stock/";

        public PortfolioService(IHttpClientFactory factory, IRokiConfig config, IMongoService mongo)
        {
            _factory = factory;
            _config = config;
            _mongo = mongo;
        }

        public async Task<Dictionary<string, Investment>> GetUserPortfolio(IUser user, ulong guildId)
        {
            return (await _mongo.Context.GetOrAddUserAsync(user, guildId).ConfigureAwait(false)).Data[guildId].Portfolio;
        }

        public async Task<decimal> GetPortfolioValue(Dictionary<string, Investment> portfolio)
        {
            var value = 0m;
            foreach ((string symbol, Investment investment) in portfolio)
            {
                decimal price = await GetStockPrice(symbol).ConfigureAwait(false);
                value += price * investment.Shares;
            }

            return value;
        }

        private async Task<decimal> GetStockPrice(string symbol)
        {
            using HttpClient http = _factory.CreateClient();
            string result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/quote/latestPrice?token={_config.IexToken}").ConfigureAwait(false);
            return decimal.Parse(result);
        }
    }
}