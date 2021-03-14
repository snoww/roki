namespace Roki.Services.Database.Models
{
    public class XpReward
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public int Level { get; set; }
        public string Description { get; set; }
        
        // will be the delimited by colons
        // e.g. currency reward
        // currency;10000
        // e.g. role reward
        // role;11111111111111111
        public string Details { get; set; }
    }
}
