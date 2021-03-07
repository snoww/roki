using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class Event
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ulong HostId { get; set; }
        public DateTime StartDate { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public ulong[] Participants { get; set; }
        public ulong[] Undecided { get; set; }
        public bool Deleted { get; set; }

        public virtual Guild Guild { get; set; }
    }
}
