using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Roki.Services.Database;

namespace Roki.Services
{
    public interface IMongoService
    {
        public IMongoDatabase Database { get; }
        public IMongoContext Context { get; }
    }
    
    public sealed class MongoService : IMongoService
    {
        public IMongoDatabase Database { get; }
        public IMongoContext Context { get; }

        public MongoService(DbConfig config)
        {
            var conventions = new ConventionPack {new LowerCaseElementNameConvention()};
            ConventionRegistry.Register("LowerCaseElementName", conventions, t => true);
            BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
            BsonSerializer.RegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

            Database = new MongoClient($"mongodb://{config.Username}:{config.Password}@{config.Host}/?authSource=admin").GetDatabase(config.Database);
            Context = new MongoContext(Database);
        }
    }
    
    public class LowerCaseElementNameConvention : IMemberMapConvention 
    {
        public void Apply(BsonMemberMap memberMap) 
        {
            memberMap.SetElementName(memberMap.MemberName.ToLower());
        }

        public string Name => "LowerCaseElementNameConvention";
    }
}