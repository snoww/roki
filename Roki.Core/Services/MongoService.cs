using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Roki.Services.Database.Core;

namespace Roki.Services
{
    public interface IMongoService
    {
        public IMongoDatabase Database { get; }
    }
    
    public sealed class MongoService : IMongoService
    {
        public IMongoDatabase Database { get; }

        public MongoService()
        {
            var conventions = new ConventionPack {new LowerCaseElementNameConvention()};
            ConventionRegistry.Register("LowerCaseElementName", conventions, t => true);

            Database = new MongoClient().GetDatabase("roki");
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