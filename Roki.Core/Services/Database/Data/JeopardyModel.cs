using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("airdates", Schema = "data")]
    public class AirDate : DbEntity
    {
        [Key]
        public int Game { get; set; }
        public string Airdate { get; set; }
    }
    
    [Table("categories", Schema = "data")]
    public class Categories : DbEntity
    {
        [Key]
        public int Id { get; set; }
        public string Category { get; set; }
    }
    
    [Table("classifications", Schema = "data")]
    public class Classification : DbEntity
    {
        [Key]
        public int ClueId { get; set; }
        public int CategoryId { get; set; }
    }

    [Table("clues", Schema = "data")]
    public class Clues : DbEntity
    {
        [Key]
        public int Id { get; set; }
        public int Game { get; set; }
        public int Round { get; set; }
        public int Value { get; set; }
    }
    
    [Table("documents", Schema = "data")]
    public class Documents : DbEntity
    {
        [Key]
        public int Id { get; set; }
        public string Clue { get; set; }
        public string Answer { get; set; }
    }
}