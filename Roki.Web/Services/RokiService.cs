using System.Threading.Tasks;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Roki.Web.Models;

namespace Roki.Web.Services
{
    public class RokiService
    {
        private IMongoCollection<Guild> _guilds;

        public RokiService(IRokiDatabaseSettings settings)
        {
            var conventions = new ConventionPack {new LowerCaseElementNameConvention()};
            ConventionRegistry.Register("LowerCaseElementName", conventions, _ => true);
            var client = new MongoClient(settings.ConnectionString);
            IMongoDatabase database = client.GetDatabase(settings.DatabaseName);
            _guilds = database.GetCollection<Guild>("guilds");
        }

        public async Task<Guild> GetRokiGuild(ulong guildId)
        {
            return await _guilds.Find(x => x.Id == guildId).FirstOrDefaultAsync();
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