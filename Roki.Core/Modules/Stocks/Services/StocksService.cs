using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Roki.Extensions;
using Roki.Modules.Stocks.Models;
using Roki.Services;

namespace Roki.Modules.Stocks.Services
{
    public class StocksService : IRokiService
    {
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _httpFactory;
        private const string IexStocksUrl = "https://cloud.iexapis.com/v1/stock";

        public StocksService(IRokiConfig config, IHttpClientFactory httpFactory)
        {
            _config = config;
            _httpFactory = httpFactory;
        }

        public async Task<Company> GetCompanyAsync(string symbol)
        {
            using HttpClient http = _httpFactory.CreateClient();
            try
            {
                string result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/company?token={_config.IexToken}").ConfigureAwait(false);
                return result.Deserialize<Company>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<StockNews[]> GetNewsAsync(string symbol)
        {
            using HttpClient http = _httpFactory.CreateClient();
            try
            {
                string result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/news?token={_config.IexToken}").ConfigureAwait(false);
                return result.Deserialize<StockNews[]>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<Quote> GetQuoteAsync(string symbol)
        {
            using HttpClient http = _httpFactory.CreateClient();
            try
            {
                string result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/quote?token={_config.IexToken}").ConfigureAwait(false);
                return result.Deserialize<Quote>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> GetLogoAsync(string symbol)
        {
            using HttpClient http = _httpFactory.CreateClient();
            using JsonDocument json = await JsonDocument.ParseAsync(await http.GetStreamAsync($"{IexStocksUrl}/{symbol}/logo?token={_config.IexToken}"));
            return json.RootElement.GetProperty("url").GetString();
        }
    }
}