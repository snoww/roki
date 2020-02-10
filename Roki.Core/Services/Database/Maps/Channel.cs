using MongoDB.Bson.Serialization.Attributes;

namespace Roki.Services.Database.Maps
{
    public class Channel
    {
        [BsonId]
        public ulong ChannelId { get; set; }
        public string Name { get; set; }
        public ulong GuildId { get; set; }
        public string GuildName { get; set; }
        public bool IsNsfw { get; set; } = false;
        public bool CurrencyGeneration { get; set; } = true;
        public bool XpGain { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public bool Logging { get; set; } = false;
    }
}