using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Microsoft.Extensions.Options;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Stocks.Models;
using Roki.Services;

namespace Roki.Modules.Stocks.Services
{
    public class StocksService : IRokiService
    {
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _httpFactory;
        private const string IexStocksUrl = "https://cloud.iexapis.com/stable/stock";

        public StocksService(IRokiConfig config, IHttpClientFactory httpFactory)
        {
            _config = config;
            _httpFactory = httpFactory;
        }

        public async Task<Company> GetCompanyAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            try
            {
                var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/company?token={_config.IexToken}").ConfigureAwait(false);
                return result.Deserialize<Company>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<StockStats> GetStockStatsAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            try
            {
                var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/stats?token={_config.IexToken}").ConfigureAwait(false);
                return result.Deserialize<StockStats>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<StockNews[]> GetNewsAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            try
            {
                var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/news?token={_config.IexToken}").ConfigureAwait(false);
                return result.Deserialize<StockNews[]>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<Quote> GetQuoteAsync(string symbol)
        {
            using var http = _httpFactory.CreateClient();
            try
            {
                var result = await http.GetStringAsync($"{IexStocksUrl}/{symbol}/quote?token={_config.IexToken}").ConfigureAwait(false);
                return result.Deserialize<Quote>();
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

        public void GenerateChartAsync(string symbol, string options)
        {
            using var proc = new Process {StartInfo = {FileName = "./scripts/image.py", UseShellExecute = false, Arguments = $"{symbol} {options}"}};
            proc.Start();
            proc.WaitForExit();
        }
    }
}