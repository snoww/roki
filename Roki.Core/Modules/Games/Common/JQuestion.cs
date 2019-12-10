namespace Roki.Modules.Games.Common
{
    public class JQuestion
    {
        public string Category { get; set; }
        public string Clue { get; set; }
        public string Answer { get; set; }
        public int Value { get; set; }
        public bool Available { get; set; } = true;
    }
}