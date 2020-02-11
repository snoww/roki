using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Maps
{
    public class User
    {
        public ulong Id { get; set; }
        public string Username { get; set; }
        public int Discriminator { get; set; }
        public string AvatarId { get; set; }
        public int Xp { get; set; } = 0;
        public DateTimeOffset LastLevelUp { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset LastXpGain { get; set; } = DateTimeOffset.MinValue;
        public string Notification { get; set; } = "dm";
        public long Currency { get; set; } = 0;
        public List<Item> Inventory { get; set; } = new List<Item>();
        public List<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public decimal InvestingAccount { get; set; } = 50000;
        public List<Investment> Portfolio { get; set; } = new List<Investment>();
    }
    
    public class Item
    {
        public Guid Id { get; set; }
        public ulong GuildId { get; set; }
        public int Quantity { get; set; }
    }

    public class Investment
    {
        public string Symbol { get; set; }
        public string Position { get; set; }
        public long Shares { get; set; }
        public DateTimeOffset? InterestDate { get; set; }
    }

    public class Subscription
    {
        public Guid Id { get; set; }
        public ulong GuildId { get; set; }
        public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;
        public int Length { get; set; }
    }
}