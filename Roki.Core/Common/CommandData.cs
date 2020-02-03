namespace Roki.Common
{
    public class CommandData
    {
        public string Aliases { get; set; } = string.Empty;
        public string Description { get; set; }
        public string[] Usage { get; set; }
    }
}