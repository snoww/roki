using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Roki.Core.Services;

namespace Roki.Modules.Stocks.Services
{
    public class TradingService : IRService
    {
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly DbService _db;
        private const string IexStocksUrl = "https://cloud.iexapis.com/stable/stock/";

        public TradingService(IHttpClientFactory httpFactory, IRokiConfig config, DbService db)
        {
            _httpFactory = httpFactory;
            _config = config;
            _db = db;
        }

        public async Task<decimal?> GetLatestPriceAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/quote/latestPrice?token={_config.IexToken}").ConfigureAwait(false);
            var success = decimal.TryParse(result, out var price);
            if (!success) return null;
            return price;
        }

        public async Task<bool> UpdateInvAccountAsync(ulong userId, long amount)
        {
            using var uow = _db.GetDbContext();
            var success = await uow.DUsers.UpdateInvestingAccountAsync(userId, amount).ConfigureAwait(false);
            return success;
        }
    }
}