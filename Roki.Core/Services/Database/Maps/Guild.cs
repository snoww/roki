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
        public List<XpReward> XpRewards { get; set; } = new();
        public List<Listing> Store { get; set; } = new();
        public GuildConfig Config { get; set; } = new();
    }
    
    public class XpReward
    {
        public ObjectId Id { get; set; }
        public int Level { get; set; }
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

    public class GuildConfig : Properties
    {
        // default settings for guild
        // i.e. when a new channel is created, these settings are inherited
        public bool Logging { get; set; } = false;
        public bool CurrencyGeneration { get; set; } = false;
        public bool XpGain { get; set; } = false;
        public Dictionary<string, bool> Modules { get; set; } = new();
        public Dictionary<string, bool> Commands { get; set; } = new();
    }
}