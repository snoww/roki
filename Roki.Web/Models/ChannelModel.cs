using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roki.Web.Models
{
    public class Channel
    {
        public ulong Id { get; }
        public ulong GuildId { get; }
        public string Name { get; }
        public DateTime? DeletedDate { get; set; }

        public virtual ChannelConfig ChannelConfig { get; set; }

        public Channel(ulong id, ulong guildId, string name)
        {
            Id = id;
            GuildId = guildId;
            Name = name;
        }
    }
    
    public class ChannelConfig
    {
        public ulong ChannelId { get; set; }
        public bool Logging { get; }
        public bool CurrencyGen { get; }
        public bool XpGain { get; }

        [JsonIgnore]
        public virtual Channel Channel { get; set; }

        public ChannelConfig(bool logging, bool currencyGen, bool xpGain)
        {
            Logging = logging;
            CurrencyGen = currencyGen;
            XpGain = xpGain;
        }
    }
}