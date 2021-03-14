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
        // public decimal Margin { get; set; }
        public DateTime? InterestDate { get; set; }

        public virtual User User { get; set; }
        public virtual ICollection<Trade> Trades { get; set; } = new List<Trade>();

        public Investment(ulong userId, ulong guildId, string symbol, long shares)
        {
            UserId = userId;
            GuildId = guildId;
            Symbol = symbol;
            Shares = shares;
        }
    }
}
