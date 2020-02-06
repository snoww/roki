using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Maps
{
    public class User
    {
        public ulong Id { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
        public string AvatarId { get; set; }
        public int Xp { get; set; }
        public DateTimeOffset LastLevelUp { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset LastXpGain { get; set; } = DateTimeOffset.MinValue;
        public string Notification { get; set; } = "dm";
        public long Currency { get; set; }
        public List<Item> Inventory { get; set; }
        public decimal InvestingAccount { get; set; } = 50000;
        public List<Investment> Portfolio { get; set; }
    }
    
    public class Item
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
    }

    public class Investment
    {
        public string Symbol { get; set; }
        public string Position { get; set; }
        public long Shares { get; set; }
        public DateTimeOffset? InterestDate { get; set; }
    }
}