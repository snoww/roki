using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("trades")]
    public class Trades : DbEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("user_id")]
        public ulong UserId { get; set; }
        [Column("symbol")]
        public string Symbol { get; set; }
        [Column("position")]
        public string Position { get; set; }
        [Column("action")]
        public string Action { get; set; }
        [Column("shares")]
        public long Shares { get; set; }
        [Column("price")]
        public decimal Price { get; set; }
        [Column("transaction_date")]
        public DateTimeOffset TransactionDate { get; set; }
    }
}