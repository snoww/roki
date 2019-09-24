using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("messages")]
    public class DMessage : DbEntity
    {
        public ulong AuthorId { get; set; }
        public string Author { get; set; }
        public ulong ChannelId { get; set; }
        public string Channel { get; set; }
        public ulong? GuildId { get; set; }
        public string Guild { get; set; }
        public ulong MessageId { get; set; }
        public string Content { get; set; }
        public DateTime? EditedTimestamp { get; set; }
        public DateTime Timestamp { get; set; }
    }
}