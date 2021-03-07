using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class Subscription
    {
        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }
        public int ItemId { get; set; }
        public DateTime Expiry { get; set; }
    }
}
