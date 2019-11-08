using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Roki.Core.Services;
using Roki.Modules.Stocks.Models;

namespace Roki.Modules.Stocks.Services
{
    public class StocksService : IRService
    {
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _httpFactory;
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions{PropertyNameCaseInsensitive = true};
        private const string IexStocksUrl = "https://cloud.iexapis.com/stable/stock/";

        public StocksService(IRokiConfig config, IHttpClientFactory httpFactory)
        {
            _config = config;
            _httpFactory = httpFactory;
        }

        public async Task<Company> GetCompanyAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/company?token={_config.IexToken}").ConfigureAwait(false);
            try
            {
                return JsonSerializer.Deserialize<Company>(result);
            }
            catch
            {
                return null;
            }
        }

        public async Task<StockStats> GetStockStatsAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/stats?token={_config.IexToken}").ConfigureAwait(false);
            try
            {
                return JsonSerializer.Deserialize<StockStats>(result);
            }
            catch
            {
                return null;
            }
        }

        public async Task<StockNews[]> GetNewsAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/news?token={_config.IexToken}").ConfigureAwait(false);
            try
            {
                return JsonSerializer.Deserialize<StockNews[]>(result);
            }
            catch
            {
                return null;
            }
        }

        public async Task<Quote> GetQuoteAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/quote?token={_config.IexToken}").ConfigureAwait(false);
            try
            {
                return JsonSerializer.Deserialize<Quote>(result);
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> GetLogoAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/logo?token={_config.IexToken}").ConfigureAwait(false);
            using var json = JsonDocument.Parse(result);
            return json.RootElement.GetProperty("url").GetString();
        }
    }
}