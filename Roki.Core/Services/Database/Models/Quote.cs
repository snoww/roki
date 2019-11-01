using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("quotes")]
    public class Quote : DbEntity
    {
        public ulong GuildId { get; set; }

        [Required]
        public string Keyword { get; set; }

        [Required]
        public string AuthorName { get; set; }

        public ulong AuthorId { get; set; }

        [Required]
        public string Text { get; set; }
        public string Context { get; set; }
        
        public DateTime? DateAdded { get; set; } = DateTime.UtcNow;

        public int UseCount { get; set; }
    }

    public enum OrderType
    {
        Id = -1,
        Keyword = -2
    }
}