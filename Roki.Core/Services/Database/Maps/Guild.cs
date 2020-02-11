using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace Roki.Services.Database.Maps
{
    public class Guild
    {
        [BsonId]
        public ulong Id { get; set; }
        public string Name { get; set; }
        public string IconId { get; set; }
        public ulong OwnerId { get; set; }
        public int ChannelCount { get; set; }
        public int MemberCount { get; set; }
        public int EmoteCount { get; set; }
        public string RegionId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool Available { get; set; } = true;
        public List<XpReward> XpRewards { get; set; } = new List<XpReward>();
    }
    
    public class XpReward
    {
        public string Id { get; set; }
        public int XpLevel { get; set; }
        public string Type { get; set; }
        public string Reward { get; set; }
    }
}