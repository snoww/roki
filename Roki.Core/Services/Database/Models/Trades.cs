using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("trades")]
    public class Trades : DbEntity
    {
        [Column("user_id")]
        public ulong UserId { get; set; }
        public string Symbol { get; set; }
        public string Position { get; set; }
        public string Action { get; set; }
        public long Shares { get; set; }
        public decimal Price { get; set; }
        [Column("transaction_date")]
        public DateTimeOffset TransactionDate { get; set; }
    }
}