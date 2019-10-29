using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("users")]
    public class DUser : DbEntity
    {
        public ulong UserId { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
        public string AvatarId { get; set; }
        public int TotalXp { get; set; }
        public DateTime LastLevelUp { get; set; } = DateTime.UtcNow;
        public DateTime LastXpGain { get; set; } = DateTime.MinValue;
        public byte NotificationLocation { get; set; } = 1;
        public long Currency { get; set; } = 0;
        public string Inventory { get; set; } = null;
        
        public override bool Equals(object obj) => 
            obj is DUser dUser && dUser.UserId == UserId;
        
        public override int GetHashCode() => 
            UserId.GetHashCode();
        
        public override string ToString() => 
            Username + "#" + Discriminator;
    }

    public class Inventory
    {
        public int Mute { get; set; } = 0;
        public int Timeout { get; set; } = 0;
        public int DeleteMessage { get; set; } = 0;
        public int SlowMode { get; set; } = 0;
    }
}