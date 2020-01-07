using System.Text.Json.Serialization;

namespace Roki.Modules.Searches.Models
{
    public class XkcdModel
    {
        public string Month { get; set; }
        public int Num { get; set; }
        public string Year { get; set; }
        [JsonPropertyName("safe_title")]
        public string Title { get; set; }
        public string Img { get; set; }
    }
}