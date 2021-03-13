using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roki.Modules.Searches.Models
{
    public class Pokemon
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; }
        public int Num { get; set; }
        public string Species { get; set; }
        public List<string> Types { get; set; }
        public Dictionary<string, double> GenderRatio { get; set; }
        public Dictionary<string, int> BaseStats { get; set; }
        public Dictionary<string, string> Abilities { get; set; }
        public double Height { get; set; }
        public double Weight { get; set; }
        public string Color { get; set; }
        public string Prevo { get; set; }
        public List<string> Evos { get; set; }
        public List<string> EggGroups { get; set; }
    }
    
    public class Ability
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public string ShortDesc { get; set; }
        public double Rating { get; set; }
    }

    public class Move
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public string ShortDesc { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public int? Accuracy { get; set; }
        public int Power { get; set; }
        public int Pp { get; set; }
        public int Priority { get; set; }
    }
}