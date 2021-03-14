using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class StoreItem
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong SellerId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Details { get; set; }
        public string Category { get; set; }
        public int Duration { get; set; }
        public int? Price { get; set; }
        public int? Quantity { get; set; }

        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public virtual ICollection<InventoryItem> Inventory { get; set; } = new List<InventoryItem>();
    }
}
