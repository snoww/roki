using System;

namespace Roki.Core.Services.Database.Models
{
    public class DUser : DbEntity
    {
        public ulong UserId { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
        public string AvatarId { get; set; }
        public int TotalXp { get; set; }
        public DateTime LastLevelUp { get; set; }
        public DateTime LastXpGain { get; set; }
        public long CurrencyAmount { get; set; }
        
        public override bool Equals(object obj) => 
            obj is DUser dUser && dUser.UserId == UserId;
        
        public override int GetHashCode() => 
            UserId.GetHashCode();
        
        public override string ToString() => 
            Username + "#" + Discriminator;
    }
}