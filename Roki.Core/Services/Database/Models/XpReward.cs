namespace Roki.Services.Database.Models
{
    public class XpReward
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public int Level { get; set; }
        public string Type { get; set; }
        public string Reward { get; set; }
    }
}
