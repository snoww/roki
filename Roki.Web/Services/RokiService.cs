using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Roki.Web.Models;

namespace Roki.Web.Services
{
    public class RokiService
    {
        private readonly IMongoCollection<Guild> _guilds;
        private readonly IMongoCollection<Channel> _channels;

        public RokiService(IRokiDatabaseSettings settings)
        {
            var conventions = new ConventionPack {new LowerCaseElementNameConvention()};
            ConventionRegistry.Register("LowerCaseElementName", conventions, _ => true);
            var client = new MongoClient(settings.ConnectionString);
            IMongoDatabase database = client.GetDatabase(settings.DatabaseName);
            _guilds = database.GetCollection<Guild>("guilds");
            _channels = database.GetCollection<Channel>("channels");
        }

        public async Task<Guild> GetRokiGuild(ulong guildId)
        {
            return await _guilds.Find(x => x.Id == guildId).FirstOrDefaultAsync();
        }

        public async Task<List<ChannelSummary>> GetGuildChannels(ulong guildId)
        {
            return await _channels.Find(x => x.GuildId == guildId && !x.IsDeleted)
                .SortBy(x => x.Name)
                .Project(x => new ChannelSummary
                {
                    Name = x.Name,
                    Id = x.Id
                }).ToListAsync();
        }
        
        public async Task<Channel> GetGuildChannel(ulong guildId, ulong channelId)
        {
            return await _channels.Find(x => x.Id == channelId && x.GuildId == guildId && !x.IsDeleted).FirstOrDefaultAsync();
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