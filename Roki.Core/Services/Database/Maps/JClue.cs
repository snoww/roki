using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Roki.Services.Database.Maps
{
    public class JClue
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Category { get; set; }
        public string Clue { get; set; }
        public string Answer { get; set; }
        public int Value { get; set; }
    }
}