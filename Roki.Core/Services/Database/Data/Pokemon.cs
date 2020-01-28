using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Services.Database.Data
{
    [Table("pokedex", Schema = "data")]
    public class Pokemon : DbEntity
    {
        [Key]
        public string Name { get; set; }
        public int Number { get; set; }
        public string Species { get; set; }
        [Column("type_0")]
        public string Type0 { get; set; }
        [Column("type_1")]
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
        [Column("ability_0")]
        public string Ability0 { get; set; }
        [Column("ability_1")]
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