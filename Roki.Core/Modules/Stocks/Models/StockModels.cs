using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roki.Modules.Stocks.Models
{
    public class StockStats
    {
        public decimal Week52Change { get; set; }
        public decimal Week52High { get; set; }
        public decimal Week52Low { get; set; }
        public long MarketCap { get; set; }
        public long Employees { get; set; }
        public decimal Day200MovingAvg { get; set; }
        public decimal Day50MovingAvg { get; set; }
        public decimal Float { get; set; }
        public decimal Avg10Volume { get; set; }
        public decimal Avg30Volume { get; set; }
        public decimal TtmEps { get; set; }
        public string CompanyName { get; set; }
        public long SharesOutstanding { get; set; }
        public decimal MaxChangePercent { get; set; }
        public decimal Year5ChangePercent { get; set; }
        public decimal Year2ChangePercent { get; set; }
        public decimal Year1ChangePercent { get; set; }
        public decimal YtdChangePercent { get; set; }
        public decimal Month6ChangePercent { get; set; }
        public decimal Month3ChangePercent { get; set; }
        public decimal Month1ChangePercent { get; set; }
        public decimal Day30ChangePercent { get; set; }
        public decimal Day5ChangePercent { get; set; }
        public DateTime NextEarningsDate { get; set; }
        public decimal PeRatio { get; set; }
        public decimal Beta { get; set; }
    }

    public class StockNews
    {
        public double DateTime { get; set; }
        public string Headline { get; set; }
        public string Source { get; set; }
        public string Url { get; set; }
        public string Summary { get; set; }
        public string Image { get; set; }
        public bool HasPayWall { get; set; }
    }

    public class Company
    {
        public string Symbol { get; set; }
        public string CompanyName { get; set; }
        public string Exchange { get; set; }
        public string Website { get; set; }
        public string Description { get; set; }
        public string Ceo { get; set; }
        public int Employees { get; set; }
        public string[] Tags { get; set; }
        public string Country { get; set; }
    }

    public class Quote
    {
        public string Symbol { get; set; }
        public string CompanyName { get; set; }
        public string PrimaryExchange { get; set; }
        public decimal? Open { get; set; }
        public decimal? Close { get; set; }
        public decimal? High { get; set; }
        public decimal? Low { get; set; }
        public decimal LatestPrice { get; set; }
        public string LatestTime { get; set; }
        public double LatestUpdate { get; set; }
    }
}