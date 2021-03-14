using System.Collections.Generic;
using System.Collections.Immutable;

#nullable disable

namespace Roki.Services.Database.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Round { get; set; }

        public virtual List<Clue> Clues { get; set; } = new();

        public Category(string name, int round)
        {
            Name = name;
            Round = round;
        }
    }
}
