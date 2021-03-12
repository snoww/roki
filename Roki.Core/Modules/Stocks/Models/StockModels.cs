using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roki.Modules.Stocks.Models
{
    public class StockNews
    {
        public long DateTime { get; set; }
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
        public decimal ChangePercent { get; set; }
        public decimal Change { get; set; }
        public string LatestSource { get; set; }
        public decimal LatestPrice { get; set; }
        public long LatestUpdate { get; set; }
    }
}