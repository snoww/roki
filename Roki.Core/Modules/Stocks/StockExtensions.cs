using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Roki.Modules.Stocks
{
    public static class StockExtensions
    {
        private static readonly Dictionary<string, string> ExchangeMap = new Dictionary<string, string>
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
            var rgx = new Regex(@"^\w{3}:*$");
            if (!rgx.IsMatch(symbol)) return symbol;
            var parsed = symbol.Split(":");
            var exchange = parsed[0];
            var ticker = parsed[1];
            if (ExchangeMap.ContainsKey(exchange.ToUpper()))
                return ticker + ExchangeMap[exchange.ToUpper()];
            return ticker;
        }
    }
}