using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("subscriptions")]
    public class Subscriptions : DbEntity
    {
        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }
        public int ItemId { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset EndDate { get; set; }
        
        public virtual DUser User { get; set; }
    }
}