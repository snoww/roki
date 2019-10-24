using System;

namespace Roki.Core.Services.Database.Models
{
    public class Subscriptions : DbEntity
    {
        public ulong UserId { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime EndDate { get; set; }
    }
}