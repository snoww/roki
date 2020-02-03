using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Services.Database.Core
{
    [Table("store")]
    public class Listing : DbEntity
    {
        [Key]
        public int Id { get; set; }
        public ulong SellerId { get; set; }
        public string Item { get; set; }
        public string Details { get; set; }
        public string Description { get; set; } = "-";
        public string Category { get; set; }
        public string Type { get; set; } = "OneTime";
        public int? SubscriptionDays { get; set; }
        public long Cost { get; set; }
        public int Quantity { get; set; } = 1;
        public DateTimeOffset ListDate { get; set; } = DateTimeOffset.UtcNow;
    }
}