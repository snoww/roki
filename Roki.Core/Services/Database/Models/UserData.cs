using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class UserData
    {
        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }
        public long Xp { get; set; } = 0;
        public DateTime LastLevelUp { get; set; } = DateTime.MinValue;
        public DateTime LastXpGain { get; set; } = DateTime.MinValue;

        public int NotificationLocation { get; set; } = 1;
        public long Currency { get; set; } = 1000;
        public decimal Investing { get; set; } = 5000;

        public virtual User User { get; set; }
    }
}
