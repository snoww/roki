using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("abilities", Schema = "data")]
    public class Ability : DbEntity
    {
        [Key]
        public string Id { get; set; }
        public string Name { get; set; }
        [Column("desc")]
        public string Description { get; set; }
        [Column("short_desc")]
        public string ShortDescription { get; set; }
        public float Rating { get; set; }
    }
}