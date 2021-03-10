using System;
using Roki.Services.Database.Models;

namespace Roki.Services.Database.Models
{
    public class Channel
    {
        public ulong Id { get; set; }
        public ulong GuildId { get; set; }
        public string Name { get; set; }
        public DateTime? DeletedDate { get; set; }

        public virtual ChannelConfig ChannelConfig { get; set; }

        public Channel(ulong id, ulong guildId, string name)
        {
            Id = id;
            GuildId = guildId;
            Name = name;
        }
    }
}
