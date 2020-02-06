using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Roki.Services.Database.Maps
{
    public class Transaction
    {
        [BsonId(IdGenerator = typeof(CombGuidGenerator))]
        public Guid Id { get; set; }
        public ulong From { get; set; }
        public ulong To { get; set; }
        public long Amount { get; set; }
        public string Reason { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public DateTimeOffset Date { get; set; } = DateTimeOffset.UtcNow;
    }
}