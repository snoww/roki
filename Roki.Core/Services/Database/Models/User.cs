using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("users")]
    public class User : DbEntity
    {
        [Column("user_id")]
        public ulong UserId { get; set; }
        [Column("username")]
        public string Username { get; set; }
        [Column("discriminator")]
        public string Discriminator { get; set; }
        [Column("avatar_id")]
        public string AvatarId { get; set; }
        [Column("total_xp")] 
        public int TotalXp { get; set; }
        [Column("last_level_up")]
        public DateTimeOffset LastLevelUp { get; set; } = DateTimeOffset.MinValue;
        [Column("last_xp_gain")]
        public DateTimeOffset LastXpGain { get; set; } = DateTimeOffset.MinValue;
        [Column("notification_location")]
        public string NotificationLocation { get; set; } = "dm";
        [Column("currency")]
        public long Currency { get; set; }
        [Column("inventory", TypeName = "json")]
        public string Inventory { get; set; }
        [Column("investing")]
        public decimal InvestingAccount { get; set; } = 50000;
        [Column("portfolio", TypeName = "json")]
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