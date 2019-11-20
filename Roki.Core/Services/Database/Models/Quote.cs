using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("quotes")]
    public class Quote : DbEntity
    {
        [Column("guild_id")]
        public ulong GuildId { get; set; }
        [Required]
        public string Keyword { get; set; }
        [Required]
        [Column("author_name")]
        public string AuthorName { get; set; }
        [Column("author_id")]
        public ulong AuthorId { get; set; }
        [Required]
        public string Text { get; set; }
        public string Context { get; set; }
        [Column("date_added")]
        public DateTimeOffset? DateAdded { get; set; } = DateTimeOffset.UtcNow;
        [Column("use_count")]
        public int UseCount { get; set; } = 1;
        
        public virtual DUser Author { get; set; }

    }

    public enum OrderType
    {
        Id = -1,
        Keyword = -2
    }
}