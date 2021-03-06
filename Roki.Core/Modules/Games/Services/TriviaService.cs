using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Roki.Extensions;
using Roki.Modules.Games.Common;
using Roki.Services;

namespace Roki.Modules.Games.Services
{
    public class TriviaService : IRokiService
    {
        private readonly IHttpClientFactory _http;
        public readonly ConcurrentDictionary<ulong, ulong> TriviaGames = new();
        private const string OpenTdbUrl = "https://opentdb.com/api.php";

        public TriviaService(IHttpClientFactory http)
        {
            _http = http;
        }

        public async Task<TriviaModel> GetTriviaQuestionsAsync(int category)
        {
            using var http = _http.CreateClient();
            string result;
            if (category == 0)
                result = await http.GetStringAsync($"{OpenTdbUrl}?amount=10").ConfigureAwait(false);
            else 
                result = await http.GetStringAsync($"{OpenTdbUrl}?amount=10&category={category}").ConfigureAwait(false);
                
            var questions = result.Deserialize<TriviaModel>();
            return questions;
        }

        public List<string> RandomizeAnswersOrder(string correct, string[] incorrect)
        {
            var questions = new List<string> {HttpUtility.HtmlDecode(correct)};
            questions.AddRange(incorrect.Select(HttpUtility.HtmlDecode));
            questions.Shuffle();
            if (incorrect.Length == 1)
                return questions;
            var letter = 'A';
            for (int i = 0; i < questions.Count; i++)
            {
                questions[i] = $"{letter++}. {questions[i]}";
            }
            
            return questions;
        }
    }
}