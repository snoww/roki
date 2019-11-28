using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("pokedex")]
    public class Pokemon : DbEntity
    {
        [Key]
        public string Name { get; set; }
        public int Number { get; set; }
        public string Species { get; set; }
        public string Type0 { get; set; }
        public string Type1 { get; set; }
        public float? MaleRatio { get; set; }
        public float? FemaleRatio { get; set; }
        public int Hp { get; set; }
        [Column("atk")]
        public int Attack { get; set; }
        [Column("def")]
        public int Defence { get; set; }
        [Column("spa")]
        public int SpecialAttack { get; set; }
        [Column("spd")]
        public int SpecialDefense { get; set; }
        [Column("spe")]
        public int Speed { get; set; }
        public string Ability0 { get; set; }
        public string Ability1 { get; set; }
        public string AbilityH { get; set; }
        public float Height { get; set; }
        public float Weight { get; set; }
        public string Color { get; set; }
        [Column("prevo")]
        public string PreEvolution { get; set; }
        [Column("evos")]
        public string[] Evolutions { get; set; }
        public string[] EggGroups { get; set; }
    }
}