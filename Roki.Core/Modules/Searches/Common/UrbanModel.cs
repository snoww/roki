namespace Roki.Modules.Searches.Common
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