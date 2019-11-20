using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("subscriptions")]
    public class Subscriptions : DbEntity
    {
        [Column("user_id")]
        public ulong UserId { get; set; }
        [Column("guild_id")]
        public ulong GuildId { get; set; }
        [Column("item_id")]
        public int ItemId { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        [Column("start_date")]
        public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;
        [Column("end_date")]
        public DateTimeOffset EndDate { get; set; }
        
        public virtual DUser User { get; set; }
    }
}