using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("store")]
    public class Listing : DbEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("seller_id")]
        public ulong SellerId { get; set; }
        [Column("item")]
        public string ItemName { get; set; }
        [Column("details")]
        public string ItemDetails { get; set; }
        [Column("description")]
        public string Description { get; set; } = "-";
        [Column("category")]
        public string Category { get; set; }
        [Column("type")]
        public string Type { get; set; } = "OneTime";
        [Column("subscription_days")]
        public int? SubscriptionDays { get; set; }
        [Column("cost")]
        public long Cost { get; set; }
        [Column("quantity")]
        public int Quantity { get; set; } = 1;
        [Column("list_date")]
        public DateTimeOffset ListDate { get; set; } = DateTimeOffset.UtcNow;
    }
}