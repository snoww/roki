using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("subscriptions")]
    public class Subscriptions : DbEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("user_id")]
        public ulong UserId { get; set; }
        [Column("guild_id")]
        public ulong GuildId { get; set; }
        [Column("item_id")]
        public int ItemId { get; set; }
        [Column("type")]
        public string Type { get; set; }
        [Column("description")]
        public string Description { get; set; }
        [Column("start_date")]
        public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;
        [Column("end_date")]
        public DateTimeOffset EndDate { get; set; }
    }
}