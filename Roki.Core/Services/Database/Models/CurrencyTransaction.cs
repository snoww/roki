using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("transactions")]
    public class CurrencyTransaction : DbEntity
    {
        public long Amount { get; set; }
        public string Reason { get; set; }
        public ulong UserIdTo { get; set; }
        public string UserIdFrom { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    }
}