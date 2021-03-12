using System.Text.Json.Serialization;

namespace Roki.Services.Database.Models
{
    public class GuildConfig : Properties
    {
        public ulong GuildId { get; set; }
        public bool Logging { get; set; } = false;
        public bool CurrencyGen { get; set; } = true;
        public bool XpGain { get; set; } = true;
        public long CurrencyDefault { get; set; } = 1000;
        public decimal InvestingDefault { get; set; } = 5000;

        [JsonIgnore]
        public virtual Guild Guild { get; set; }
        
        
        // todo add: help on command error option
    }
}
