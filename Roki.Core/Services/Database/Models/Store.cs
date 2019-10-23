using System;

namespace Roki.Core.Services.Database.Models
{
    public class Store : DbEntity
    {
        public ulong UserId { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public long Cost { get; set; }
        public byte Available { get; set; } = 1;
        public DateTime ExpiryDate { get; set; } = DateTime.UtcNow + TimeSpan.FromDays(7);
        public DateTime ListDate { get; set; } = DateTime.UtcNow;
    }
}