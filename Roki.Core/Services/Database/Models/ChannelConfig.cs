using System.Text.Json.Serialization;

namespace Roki.Services.Database.Models
{
    public class ChannelConfig
    {
        public ulong ChannelId { get; set; }
        public bool Logging { get; set; } = false;
        public bool CurrencyGen { get; set; } = true;
        public bool Xp { get; set; } = true;

        [JsonIgnore]
        public virtual Channel Channel { get; set; }
    }
}
