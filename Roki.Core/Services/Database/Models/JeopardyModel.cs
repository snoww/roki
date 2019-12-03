using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("airdates")]
    public class AirDate : DbEntity
    {
        [Key]
        public int Game { get; set; }
        public string Airdate { get; set; }
    }
    
    [Table("categories")]
    public class Categories : DbEntity
    {
        [Key]
        public int Id { get; set; }
        public string Category { get; set; }
    }
    
    [Table("classifications")]
    public class Classification : DbEntity
    {
        public int ClueId { get; set; }
        public int CategoryId { get; set; }
    }

    [Table("clues")]
    public class Clues : DbEntity
    {
        public int Id { get; set; }
        public int Game { get; set; }
        public int Round { get; set; }
        public int Value { get; set; }
    }
    
    [Table("documents")]
    public class Documents : DbEntity
    {
        public int Id { get; set; }
        public string Clue { get; set; }
        public string Answer { get; set; }
    }
}