using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("trades")]
    public class Trades : DbEntity
    {
        [Key]
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public string Symbol { get; set; }
        public string Position { get; set; }
        public string Action { get; set; }
        public long Shares { get; set; }
        public decimal Price { get; set; }
        public DateTimeOffset TransactionDate { get; set; }
    }
}