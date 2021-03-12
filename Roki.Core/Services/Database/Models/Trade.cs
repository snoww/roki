using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class Trade
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }
        public string Symbol { get; set; }
        public long Shares { get; set; }
        public decimal Price { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        
        public virtual Investment Investment { get; set; }

        public Trade(long shares, decimal price)
        {
            Shares = shares;
            Price = price;
        }
    }
}
