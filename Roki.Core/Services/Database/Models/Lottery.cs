using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("lottery")]
    public class Lottery : DbEntity
    {
        [Column("user_id")]
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
        [Column("lottery_id")]
        public string LotteryId { get; set; }
        [Column("date")]
        public DateTimeOffset Date { get; set; }
        
        public virtual DUser User { get; set; }
    }
}