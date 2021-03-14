using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class UserData
    {
        // todo user data guild defaults
        public UserData(ulong userId, ulong guildId)
        {
            UserId = userId;
            GuildId = guildId;
        }

        public UserData()
        {
        }

        public ulong UserId { get; }
        public ulong GuildId { get; }
        public long Xp { get; set; } = 0;
        public DateTime LastLevelUp { get; set; } = DateTime.MinValue;
        public DateTime LastXpGain { get; set; } = DateTime.MinValue;

        public int NotificationLocation { get; set; } = 1;
        public long Currency { get; set; } = 1000;
        public decimal Investing { get; set; } = 5000;

        public virtual User User { get; set; }
    }
}
