using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Roki.Modules.Stocks.Extensions
{
    public static class StockExtensions
    {
        private static readonly Dictionary<string, string> ExchangeMap = new()
        {
            {"ADS", "-DH"},
            {"TAE", "-IT"},
            {"BOM", "-IB"},
            {"KRX", "-KP"},
            {"BRU", "-BB"},
            {"ETR", "-GY"},
            {"PAR", "-FP"},
            {"LON", "-LN"},
            {"DUB", "-ID"},
            {"AMS", "-NA"},
            {"LIS", "-PL"},
            {"TSE", "-CT"},
            {"TSX", "-CV"},
            {"MEX", "-MM"},
        };
        public static string ParseStockTicker(this string symbol)
        {
            symbol = symbol.Trim();
            var rgx = new Regex(@"^\w{3}:\w+$");
            if (!rgx.IsMatch(symbol)) return symbol;
            string[] parsed = symbol.Split(":");
            string exchange = parsed[0];
            string ticker = parsed[1];
            if (ExchangeMap.ContainsKey(exchange.ToUpper()))
                return ticker.ToUpper() + ExchangeMap[exchange.ToUpper()];
            return ticker.ToUpper();
        }
    }
}