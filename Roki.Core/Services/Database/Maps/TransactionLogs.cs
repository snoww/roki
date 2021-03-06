using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Roki.Services.Database.Maps
{
    public class Transaction
    {
        [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
        public ObjectId Id { get; set; }
        public ulong? From { get; set; }
        public ulong? To { get; set; }
        public long Amount { get; set; }
        public string Reason { get; set; }
        public ulong? GuildId { get; set; }
        public ulong? ChannelId { get; set; }
        public ulong MessageId { get; set; }
    }

    public class Trade
    {
        [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
        public ObjectId Id { get; set; }
        public ulong UserId { get; set; }
        public string Ticker { get; set; }
        public string Position { get; set; }
        public string Action { get; set; }
        public long Shares { get; set; }
        public decimal Price { get; set; }
    }
}