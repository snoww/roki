using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("lottery")]
    public class Lottery : DbEntity
    {
        public ulong UserId { get; set; }
        public int Num1 { get; set; }
        public int Num2 { get; set; }
        public int Num3 { get; set; }
        public int Num4 { get; set; }
        public int Num5 { get; set; }
        public int Num6 { get; set; }
        public string LotteryId { get; set; }
        public DateTime Date { get; set; }
    }
}