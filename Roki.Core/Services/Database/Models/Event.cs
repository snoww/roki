using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class Event
    {
        public Event(ulong hostId, ulong guildId, ulong channelId, string name, string description, DateTime startDate)
        {
            GuildId = guildId;
            Name = name;
            Description = description;
            HostId = hostId;
            StartDate = startDate;
            ChannelId = channelId;
        }

        public int Id { get; set; }
        public ulong GuildId { get; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ulong HostId { get; }
        public DateTime StartDate { get; set; }
        public ulong ChannelId { get; }
        public ulong MessageId { get; set; }
        public List<string> Participants { get; set; } = new();
        public List<string> Undecided { get; set; } = new();

        public virtual Guild Guild { get; set; }
    }
}
