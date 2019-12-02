using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("lottery")]
    public class Lottery : DbEntity
    {
        public ulong UserId { get; set; }
        [Column("num_1")]
        public int Num1 { get; set; }
        [Column("num_2")]
        public int Num2 { get; set; }
        [Column("num_3")]
        public int Num3 { get; set; }
        [Column("num_4")]
        public int Num4 { get; set; }
        [Column("num_5")]
        public int Num5 { get; set; }
        [Column("num_6")]
        public int Num6 { get; set; }
        public string LotteryId { get; set; }
        public DateTimeOffset Date { get; set; }
    }
}