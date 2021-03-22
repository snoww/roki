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
        public bool Logging { get; set; }
        public bool CurrencyGen { get; set; }
        public bool XpGain { get; set; }

        [JsonIgnore]
        public virtual Channel Channel { get; set; }

        public ChannelConfig(bool logging, bool currencyGen, bool xpGain)
        {
            Logging = logging;
            CurrencyGen = currencyGen;
            XpGain = xpGain;
        }
    }

    public class ChannelConfigUpdate
    {
        [JsonPropertyName("channel_logging")]
        public string Logging { get; set; }
        [JsonPropertyName("channel_curr")]
        public string CurrencyGen { get; set; }
        [JsonPropertyName("channel_xp")]
        public string XpGain { get; set; }
    }
}