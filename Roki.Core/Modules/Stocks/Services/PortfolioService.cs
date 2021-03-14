using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;

namespace Roki.Modules.Stocks.Services
{
    public class PortfolioService : IRokiService
    {
        private readonly IRokiConfig _config;
        private readonly RokiContext _context;
        private readonly IHttpClientFactory _factory;
        private const string IexStocksUrl = "https://cloud.iexapis.com/stable/stock/";

        public PortfolioService(IHttpClientFactory factory, IRokiConfig config, RokiContext context)
        {
            _factory = factory;
            _config = config;
            _context = context;
        }

        public async Task<List<Investment>> GetUserPortfolio(ulong userId, ulong guildId)
        {
            return await _context.Investments.AsNoTracking()
                .Where(x => x.UserId == userId && x.GuildId == guildId && x.Shares != 0)
                .ToListAsync();
        }

        public async Task<decimal> GetPortfolioValue(IEnumerable<Investment> portfolio)
        {
            var value = 0m;
            foreach (Investment investment in portfolio)
            {
                decimal price = await GetStockPrice(investment.Symbol).ConfigureAwait(false);
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