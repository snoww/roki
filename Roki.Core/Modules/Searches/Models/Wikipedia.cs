namespace Roki.Modules.Searches.Models
{
    public class WikiSearch
    {
        public string Title { get; set; }
        public string Snippet { get; set; }
    }
    
    public class WikiSummary
    {
        public string Title { get; set; }
        public string Extract { get; set; }
        public string ImageUrl { get; set; }
    }
}