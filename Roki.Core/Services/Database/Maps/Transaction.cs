using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Roki.Services.Database.Maps
{
    [BsonIgnoreExtraElements]
    public class Transaction
    {
        [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public ulong? From { get; set; }
        public ulong? To { get; set; }
        public long Amount { get; set; }
        public string Reason { get; set; }
        public ulong? GuildId { get; set; }
        public ulong? ChannelId { get; set; }
        public ulong MessageId { get; set; }
    }
}