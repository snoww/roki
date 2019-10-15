using Newtonsoft.Json;

namespace Roki.Modules.Games.Common
{
    public class TriviaModel
    {
        public int ResponseCode { get; set; }
        public Results[] Results { get; set; }
    }

    public class Results
    {
        public string Category { get; set; }
        public string Type { get; set; }
        public string Difficulty { get; set; }
        public string Question { get; set; }
        [JsonProperty("correct_answer")]
        public string Correct { get; set; }
        [JsonProperty("incorrect_answers")]
        public string[] Incorrect { get; set; }
    }
}