using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("guilds")]
    public class Guild : DbEntity
    {
        [Key]
        public ulong GuildId { get; set; }
        public string Name { get; set; }
        public string IconId { get; set; }
        public ulong OwnerId { get; set; }
        public int ChannelCount { get; set; }
        public int MemberCount { get; set; }
        public int EmoteCount { get; set; }
        public string RegionId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool Available { get; set; } = true;
        
        [Column(TypeName = "json")]
        public string Users { get; set; }
        
        [Column(TypeName = "json")]
        public string XpRewards { get; set; } 
    }

    public class GuildUser
    {
        public ulong UserId { get; set; }
        public long Currency { get; set; }
        public int TotalXp { get; set; } 
        public string Inventory { get; set; }
    }

    public class XpReward
    {
        public int XpLevel { get; set; }
        public string Type { get; set; }
        public string Reward { get; set; }
    }
}