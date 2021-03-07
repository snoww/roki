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
        public string Position { get; set; }
        public string Action { get; set; }
        public long Amount { get; set; }
        public decimal Price { get; set; }
    }
}
