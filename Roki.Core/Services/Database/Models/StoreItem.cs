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
        public int? Duration { get; set; }
        public int? Price { get; set; }
        public int? Quantity { get; set; }
    }
}
