using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Roki.Services.Database.Maps
{
    public class Guild
    {
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
        public List<Listing> Store { get; set; } = new List<Listing>();
    }
    
    public class XpReward
    {
        public ObjectId Id { get; set; }
        public int XpLevel { get; set; }
        public string Type { get; set; }
        public string Reward { get; set; }
    }

    public class Listing
    {
        public ObjectId Id { get; set; }
        public ulong SellerId { get; set; }
        public string Name { get; set; }
        public string Details { get; set; }
        public string Description { get; set; } = "-";
        public string Category { get; set; }
        public string Type { get; set; } = "OneTime";
        public int? SubscriptionDays { get; set; }
        public long Cost { get; set; }
        public int Quantity { get; set; } = 1;
    }
}