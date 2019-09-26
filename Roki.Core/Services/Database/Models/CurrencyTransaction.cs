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
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    }
}