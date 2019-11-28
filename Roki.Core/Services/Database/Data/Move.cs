using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    public class Move : DbEntity
    {
        [Key]
        public string Id { get; set; }
        public string Name { get; set; }
        public int? Accuracy { get; set; }
        public int BasePower { get; set; }
        public string Category { get; set; }
        [Column("desc")]
        public string Description { get; set; }
        [Column("short_desc")]
        public string ShortDescription { get; set; }
        public int Pp { get; set; }
        public int Priority { get; set; }
        public string Type { get; set; }
        public string ContestType { get; set; }
    }
}