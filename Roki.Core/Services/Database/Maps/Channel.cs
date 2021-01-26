using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace Roki.Services.Database.Maps
{
    public class Channel
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public ulong GuildId { get; set; }
        public bool IsNsfw { get; set; }
        public bool CurrencyGeneration { get; set; } = true;
        public bool XpGain { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public bool Logging { get; set; } = false;
        public DateTimeOffset CreatedAt { get; set; }
        public ChannelConfig Config { get; set; }
    }

    public class ChannelConfig
    {
        // settings for specific channel
        // inherits guild settings by default
        public bool Logging { get; set; }
        public bool CurrencyGeneration { get; set; }
        public bool XpGain { get; set; }
        public Dictionary<string, bool> Modules { get; set; } = new();
        public Dictionary<string, bool> Commands { get; set; } = new();
    }
}