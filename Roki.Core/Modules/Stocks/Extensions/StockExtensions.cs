using System;
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
            if (!rgx.IsMatch(symbol)) return symbol.ToUpperInvariant();
            string[] parsed = symbol.Split(":");
            string exchange = parsed[0];
            string ticker = parsed[1];
            if (ExchangeMap.ContainsKey(exchange.ToUpperInvariant()))
                return ticker.ToUpperInvariant() + ExchangeMap[exchange.ToUpperInvariant()];
            return ticker.ToUpperInvariant();
        }

        public static DateTimeOffset ToEasternStandardTime(this long timestamp)
        {
            PlatformID system = Environment.OSVersion.Platform;
            return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.FromUnixTimeMilliseconds(timestamp), system == PlatformID.Unix ? "America/Toronto" : "Eastern Standard Time");
        }
    }
}