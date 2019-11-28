using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("channels")]
    public class Channel : DbEntity
    {
        [Key]
        public ulong ChannelId { get; set; }
        public string Name { get; set; }
        public ulong GuildId { get; set; }
        public string GuildName { get; set; }
        public int UserCount { get; set; }
        public bool IsNsfw { get; set; }
        public bool CurrencyGeneration { get; set; } = true;
        public bool XpGain { get; set; } = true;
        [Column("is_deleted")]
        public bool Deleted { get; set; }
        public bool Logging { get; set; } = false;
    }
}