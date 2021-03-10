using System.Text.Json.Serialization;

namespace Roki.Services.Database.Models
{
    public class ChannelConfig
    {
        public ulong ChannelId { get; set; }
        public bool Logging { get; set; } = false;
        public bool CurrencyGen { get; set; } = true;
        public bool XpGain { get; set; } = true;

        [JsonIgnore]
        public virtual Channel Channel { get; set; }

        public ChannelConfig(bool logging, bool currencyGen, bool xpGain)
        {
            Logging = logging;
            CurrencyGen = currencyGen;
            XpGain = xpGain;
        }

        public ChannelConfig()
        {
        }
    }
}
