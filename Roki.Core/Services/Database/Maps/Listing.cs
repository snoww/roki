using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Roki.Services.Database.Maps
{
    public class Listing
    {
        [BsonId(IdGenerator = typeof(CombGuidGenerator))]
        public Guid Id { get; set; }
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