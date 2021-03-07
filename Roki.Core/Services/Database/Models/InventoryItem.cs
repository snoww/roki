
namespace Roki.Services.Database.Models
{
    public class InventoryItem
    {
        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }
        public int ItemId { get; set; }
        public int Quantity { get; set; }
    }
}
