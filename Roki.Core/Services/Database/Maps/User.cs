using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Roki.Services.Database.Maps
{
    public class User
    {
        public ulong Id { get; set; }
        public string Username { get; set; }
        public int Discriminator { get; set; }
        public string AvatarId { get; set; }
        public int Xp { get; set; } = 0;
        public DateTime LastLevelUp { get; set; } = DateTime.MinValue;
        public DateTime LastXpGain { get; set; } = DateTime.MinValue;
        public string Notification { get; set; } = "dm";
        public long Currency { get; set; } = 0;
        public List<Item> Inventory { get; set; } = new List<Item>();
        public List<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public decimal InvestingAccount { get; set; } = 50000;
        public List<Investment> Portfolio { get; set; } = new List<Investment>();
    }
    
    public class Item
    {
        public ObjectId Id { get; set; }
        public ulong GuildId { get; set; }
        public int Quantity { get; set; }
    }

    public class Investment
    {
        public string Symbol { get; set; }
        public string Position { get; set; }
        public long Shares { get; set; }
        public DateTime? InterestDate { get; set; }
    }

    public class Subscription
    {
        public ObjectId Id { get; set; }
        public ulong GuildId { get; set; }
        public DateTime EndDate { get; set; }
    }
}