using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("quotes")]
    public class Quote : DbEntity
    {
        [Key]
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        [Required]
        public string Keyword { get; set; }
        [Required]
        public string AuthorName { get; set; }
        public ulong AuthorId { get; set; }
        [Required]
        public string Text { get; set; }
        public string Context { get; set; }
        public DateTimeOffset? DateAdded { get; set; } = DateTimeOffset.UtcNow;
        public int UseCount { get; set; } = 1;
        
    }

    public enum OrderType
    {
        Id = -1,
        Keyword = -2
    }
}