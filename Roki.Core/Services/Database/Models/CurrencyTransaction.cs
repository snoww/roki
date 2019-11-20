using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("transactions")]
    public class CurrencyTransaction : DbEntity
    {
        public long Amount { get; set; }
        public string Reason { get; set; }
        public string To { get; set; } = "-";
        public string From { get; set; } = "-";
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