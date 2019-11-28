using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("users")]
    public class User : DbEntity
    {
        [Key]
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
        public string AvatarId { get; set; }
        public int TotalXp { get; set; }
        public DateTimeOffset LastLevelUp { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset LastXpGain { get; set; } = DateTimeOffset.MinValue;
        public string NotificationLocation { get; set; } = "dm";
        public long Currency { get; set; }
        public string Inventory { get; set; }
        [Column("investing")]
        public decimal InvestingAccount { get; set; } = 50000;
        public string Portfolio { get; set; }
        
        public override bool Equals(object obj) => 
            obj is User dUser && dUser.UserId == UserId;
        
        public override int GetHashCode() => 
            UserId.GetHashCode();
        
        public override string ToString() => 
            Username + "#" + Discriminator;
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