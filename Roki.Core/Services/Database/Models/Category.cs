using System.Collections.Generic;

#nullable disable

namespace Roki.Services.Database.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Round { get; set; }

        public virtual ICollection<Clue> Clues { get; set; } = new List<Clue>();

        public Category(string name, int round)
        {
            Name = name;
            Round = round;
        }
    }
}
