using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace Roki.Services.Database.Maps
{
    public class User
    {
        public ulong Id { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
        public string AvatarId { get; set; }
        [BsonDictionaryOptions(DictionaryRepresentation.Document)]
        public Dictionary<string, UserData> Data = new();
    }

    public class UserData
    {
        public int Xp { get; set; } = 0;
        public DateTime LastLevelUp { get; set; } = DateTime.MinValue;
        public DateTime LastXpGain { get; set; } = DateTime.MinValue;
        public string Notification { get; set; } = "dm";
        public long Currency { get; set; } = 1000;
        [BsonDictionaryOptions(DictionaryRepresentation.Document)]
        public Dictionary<ObjectId, Item> Inventory { get; set; } = new();
        [BsonDictionaryOptions(DictionaryRepresentation.Document)]
        public Dictionary<ObjectId, Subscription> Subscriptions { get; set; } = new();
        public decimal InvestingAccount { get; set; } = 1000;
        [BsonDictionaryOptions(DictionaryRepresentation.Document)]
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