namespace Roki.Web.Models
{
    public interface IRokiDatabaseSettings
    {
        string DatabaseName { get; set; }
        string ConnectionString { get; set; }
    }
    
    public class RokiDatabaseSettings : IRokiDatabaseSettings
    {
        public string DatabaseName { get; set; }
        public string ConnectionString { get; set; }
    }
}