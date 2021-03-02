using System;
using System.Collections.Generic;

namespace Roki.Web.Models
{
    public class ChannelSummary
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
    }
    
    public class Channel
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public ulong GuildId { get; set; }
        public bool IsNsfw { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTimeOffset CreatedAt { get; set; }
        public ChannelConfig Config { get; set; }
    }

    public class ChannelConfig
    {
        public bool Logging { get; set; }
        public bool CurrencyGeneration { get; set; }
        public bool XpGain { get; set; }
        public Dictionary<string, bool> Modules { get; set; } = new();
        public Dictionary<string, bool> Commands { get; set; } = new();
    }
}