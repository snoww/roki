using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class Investment
    {
        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }
        public string Symbol { get; set; }
        public long Shares { get; set; }
        public int[] Trades { get; set; }
        public DateTime? InterestDate { get; set; }
    }
}
