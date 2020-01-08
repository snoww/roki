namespace Roki.Modules.Searches.Models
{
    public class UrbanResponse
    {
        public UrbanModel[] List { get; set; }
    }

    public class UrbanModel
    {
        public string Word { get; set; }
        public string Definition { get; set; }
        public string Permalink { get; set; }
    }
}