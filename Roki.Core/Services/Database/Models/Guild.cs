using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class Guild
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public ulong OwnerId { get; set; }
        public List<ulong> Moderators { get; set; } = new();
        public bool Available { get; set; } = true;

        public virtual GuildConfig GuildConfig { get; set; }
        public virtual ICollection<Event> Events { get; set; } = new List<Event>();
        public virtual ICollection<StoreItem> Items { get; set; } = new List<StoreItem>();
        public virtual ICollection<Quote> Quotes { get; set; } = new List<Quote>();
        public virtual ICollection<XpReward> XpRewards { get; set; } = new List<XpReward>();
    }
}
