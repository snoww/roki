using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("abilities")]
    public class Ability : DbEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        [Column("desc")]
        public string Description { get; set; }
        [Column("short_desc")]
        public string ShortDescription { get; set; }
        public float Rating { get; set; }
    }
}