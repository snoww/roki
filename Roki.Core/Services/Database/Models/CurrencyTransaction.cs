using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("transactions")]
    public class CurrencyTransaction : DbEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("amount")]
        public long Amount { get; set; }
        [Column("reason")]
        public string Reason { get; set; }
        [Column("to")]
        public ulong To { get; set; }
        [Column("from")]
        public ulong From { get; set; }
        [Column("guild_id")]
        public ulong GuildId { get; set; }
        [Column("channel_id")]
        public ulong ChannelId { get; set; }
        [Column("message_id")]
        public ulong MessageId { get; set; }
        [Column("transaction_date")]
        public DateTimeOffset TransactionDate { get; set; } = DateTimeOffset.UtcNow;
    }
}