using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Roki.Services.Database.Maps
{
    public class Quote
    {
        [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
        public ObjectId Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong AuthorId { get; set; }
        public string Keyword { get; set; }
        public string Text { get; set; }
        public string Context { get; set; }
        public int UseCount { get; set; } = 1;
    }
}