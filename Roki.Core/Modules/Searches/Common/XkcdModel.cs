using Newtonsoft.Json;

namespace Roki.Modules.Searches.Common
{
    public class XkcdModel
    {
        public string Month { get; set; }
        public int Num { get; set; }
        public string Year { get; set; }
        [JsonProperty("safe_title")]
        public string Title { get; set; }
        public string Img { get; set; }
    }
}