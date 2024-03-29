﻿using System;
using System.Collections.Generic;

namespace Roki.Services.Database.Models
{
    public class User
    {
        public User(ulong id, string username, string discriminator, string avatar)
        {
            Id = id;
            Username = username;
            Discriminator = discriminator;
            Avatar = avatar;
        }
        
        public User(string username, string discriminator, string avatar)
        {
            Username = username;
            Discriminator = discriminator;
            Avatar = avatar;
        }

        public ulong Id { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
        public string Avatar { get; set; }

        public virtual ICollection<InventoryItem> Inventory { get; set; } = new List<InventoryItem>();
        public virtual ICollection<Investment> Investments { get; set; } = new List<Investment>();
        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public virtual ICollection<UserData> UserData { get; set; } = new List<UserData>();
    }
}
