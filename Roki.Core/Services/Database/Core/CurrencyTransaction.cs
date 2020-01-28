using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Services.Database.Core
{
    [Table("transactions")]
    public class CurrencyTransaction : DbEntity
    {
        [Key]
        public int Id { get; set; }
        public long Amount { get; set; }
        public string Reason { get; set; }
        public ulong To { get; set; }
        public ulong From { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public DateTimeOffset TransactionDate { get; set; } = DateTimeOffset.UtcNow;
    }
}