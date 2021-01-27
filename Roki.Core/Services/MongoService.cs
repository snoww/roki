using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;
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
            ConventionRegistry.Register(
                "LowerCaseElementName", 
                conventions, 
                _ => true);
            ConventionRegistry.Register(
                "DictionaryRepresentationConvention",
                new ConventionPack {new DictionaryRepresentationConvention(DictionaryRepresentation.ArrayOfArrays)},
                _ => true);
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

    public class DictionaryRepresentationConvention : ConventionBase, IMemberMapConvention
    {
        private readonly DictionaryRepresentation _dictionaryRepresentation;

        public DictionaryRepresentationConvention(DictionaryRepresentation dictionaryRepresentation = DictionaryRepresentation.ArrayOfArrays)
        {
            // see http://mongodb.github.io/mongo-csharp-driver/2.2/reference/bson/mapping/#dictionary-serialization-options

            _dictionaryRepresentation = dictionaryRepresentation;
        }

        public void Apply(BsonMemberMap memberMap)
        {
            memberMap.SetSerializer(ConfigureSerializer(memberMap.GetSerializer(), Array.Empty<IBsonSerializer>()));
        }

        private IBsonSerializer ConfigureSerializer(IBsonSerializer serializer, IBsonSerializer[] stack)
        {
            if (serializer is IDictionaryRepresentationConfigurable dictionaryRepresentationConfigurable)
            {
                serializer = dictionaryRepresentationConfigurable.WithDictionaryRepresentation(_dictionaryRepresentation);
            }

            if (serializer is IChildSerializerConfigurable childSerializerConfigurable)
            {
                if (!stack.Contains(childSerializerConfigurable.ChildSerializer))
                {
                    var newStack = stack.Union(new[] {serializer}).ToArray();
                    var childConfigured = ConfigureSerializer(childSerializerConfigurable.ChildSerializer, newStack);
                    return childSerializerConfigurable.WithChildSerializer(childConfigured);
                }
            }

            return serializer;
        }
    }
}