using System;

namespace Roki.Core.Services.Database.Models
{
    public class Listing : DbEntity
    {
        public ulong SellerId { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; } = "-";
        public string Type { get; set; }
        public long Cost { get; set; }
        public int Quantity { get; set; } = 1;
        public DateTime ListDate { get; set; } = DateTime.UtcNow;
    }
}