using System;
using System.Collections.Generic;
using Roki.Services.Database.Models;

namespace Roki.Services.Database.Models
{
    public class ChannelConfig
    {
        public ulong ChannelId { get; set; }
        public bool Logging { get; set; }
        public bool Currency { get; set; }
        public bool Xp { get; set; }

        public virtual Channel Channel { get; set; }
    }
}
