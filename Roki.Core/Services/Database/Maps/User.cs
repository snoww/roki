using System;
using System.Collections.Generic;
using MongoDB.Bson;

namespace Roki.Services.Database.Maps
{
    public class User
    {
        public ulong Id { get; set; }
        public string Username { get; set; }
        public int Discriminator { get; set; }
        public string AvatarId { get; set; }
        // public int Xp { get; set; } = 0;
        // public DateTime LastLevelUp { get; set; } = DateTime.MinValue;
        // public DateTime LastXpGain { get; set; } = DateTime.MinValue;
        // public string Notification { get; set; } = "dm";
        // public long Currency { get; set; } = 1000;
        // public List<Item> Inventory { get; set; } = new();
        // public List<Subscription> Subscriptions { get; set; } = new();
        // public decimal InvestingAccount { get; set; } = 1000;
        // public List<Investment> Portfolio { get; set; } = new();
        public Dictionary<ulong, UserData> Data = new();
    }

    public class UserData
    {
        public int Xp { get; set; } = 0;
        public DateTime LastLevelUp { get; set; } = DateTime.MinValue;
        public DateTime LastXpGain { get; set; } = DateTime.MinValue;
        public string Notification { get; set; } = "dm";
        public long Currency { get; set; } = 1000;
        public Dictionary<ObjectId, Item> Inventory { get; set; } = new();
        public Dictionary<ObjectId, Subscription> Subscriptions { get; set; } = new();
        public decimal InvestingAccount { get; set; } = 1000;
        public Dictionary<string, Investment> Portfolio { get; set; } = new();
    } 

    public class Item
    {
        public int Quantity { get; set; }
    }

    public class Investment
    {
        public string Position { get; set; }
        public long Shares { get; set; }
        public DateTime? InterestDate { get; set; }
    }

    public class Subscription
    {
        public DateTime EndDate { get; set; }
    }
}