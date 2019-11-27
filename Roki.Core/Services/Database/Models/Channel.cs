using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("channels")]
    public class Channel : DbEntity
    {
        [Column("channel_id")]
        public ulong ChannelId { get; set; }
        [Column("name")]
        public string Name { get; set; }
        [Column("guild_id")]
        public ulong GuildId { get; set; }
        [Column("guild_name")]
        public string GuildName { get; set; }
        [Column("user_count")]
        public int UserCount { get; set; }
        [Column("is_nsfw")]
        public bool IsNsfw { get; set; }
        [Column("currency_generation")] 
        public bool CurrencyGeneration { get; set; } = true;
        [Column("xp_gain")] 
        public bool XpGain { get; set; } = true;
        [Column("is_deleted")]
        public bool Deleted { get; set; }
        [Column("logging")] 
        public bool Logging { get; set; } = false;
    }
}