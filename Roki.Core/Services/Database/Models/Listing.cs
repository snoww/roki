using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("store")]
    public class Listing : DbEntity
    {
        [Column("seller_id")]
        public ulong SellerId { get; set; }
        [Column("name")]
        public string ItemName { get; set; }
        [Column("details")]
        public string ItemDetails { get; set; }
        public string Description { get; set; } = "-";
        public string Category { get; set; }
        public string Type { get; set; } = "OneTime";
        [Column("subscription_days")]
        public int? SubscriptionDays { get; set; }
        public long Cost { get; set; }
        public int Quantity { get; set; } = 1;
        [Column("list_date")]
        public DateTimeOffset ListDate { get; set; } = DateTimeOffset.UtcNow;
        
        public virtual DUser Seller { get; set; }
    }
}