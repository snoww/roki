using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("guilds")]
    public class Guild : DbEntity
    {
        [Column("guild_id")]
        public ulong GuildId { get; set; }
        [Column("name")]
        public string Name { get; set; }
        [Column("icon_id")]
        public string IconId { get; set; }
        [Column("owner_id")]
        public string OwnerId { get; set; }
        [Column("channel_count")]
        public int ChannelCount { get; set; }
        [Column("member_count")]
        public int MemberCount { get; set; }
        [Column("emote_count")]
        public int EmoteCount { get; set; }
        [Column("region_id")]
        public string RegionId { get; set; }
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
    }
}