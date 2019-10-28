using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roki.Modules.Games.Common
{
    public class TriviaModel
    {
        public int ResponseCode { get; set; }
        public List<Results> Results { get; set; }
    }

    public class Results
    {
        public string Category { get; set; }
        public string Type { get; set; }
        public string Difficulty { get; set; }
        public string Question { get; set; }
        [JsonPropertyName("correct_answer")]
        public string Correct { get; set; }
        [JsonPropertyName("incorrect_answers")]
        public string[] Incorrect { get; set; }
    }
}