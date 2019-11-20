using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("messages")]
    public class DMessage : DbEntity
    {
        [Column("author_id")]
        public ulong AuthorId { get; set; }
        public string Author { get; set; }
        [Column("channel_id")]
        public ulong ChannelId { get; set; }
        public string Channel { get; set; }
        [Column("guild_id")]
        public ulong? GuildId { get; set; }
        public string Guild { get; set; }
        [Column("message_id")]
        public ulong MessageId { get; set; }
        public string Content { get; set; }
        [Column("edited_timestamp")]
        public DateTimeOffset? EditedTimestamp { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;
    }
}