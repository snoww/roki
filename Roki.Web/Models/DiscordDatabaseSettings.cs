namespace Roki.Web.Models
{
    public interface IDiscordDatabaseSettings
    {
        string DatabaseName { get; set; }
        string ConnectionString { get; set; }
    }
    
    public class DiscordDatabaseSettings : IDiscordDatabaseSettings
    {
        public string DatabaseName { get; set; }
        public string ConnectionString { get; set; }
    }
}