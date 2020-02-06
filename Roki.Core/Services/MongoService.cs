using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Roki.Services.Database.Core;

namespace Roki.Services
{
    public sealed class MongoService
    {
        public static MongoService Instance { get; } = new MongoService();
        
        public IMongoDatabase Database { get; }

        private MongoService()
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