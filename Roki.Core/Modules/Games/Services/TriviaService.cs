using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Games.Common;

namespace Roki.Modules.Games.Services
{
    public class TriviaService : IRService
    {
        private readonly IHttpClientFactory _http;
        public readonly ConcurrentDictionary<ulong, ulong> TriviaGames = new ConcurrentDictionary<ulong, ulong>();
        private const string OpenTdbUrl = "https://opentdb.com/api.php";

        public TriviaService(IHttpClientFactory http)
        {
            _http = http;
        }

        public async Task<TriviaModel> GetTriviaQuestionsAsync()
        {
            using (var http = _http.CreateClient())
            {
                var result = await http.GetStringAsync($"{OpenTdbUrl}?amount=10").ConfigureAwait(false);
                var questions = JsonConvert.DeserializeObject<TriviaModel>(result);
                return questions;
            }
        }

        public List<string> RandomizeAnswersOrder(string correct, string[] incorrect)
        {
            var questions = new List<string> {HttpUtility.HtmlDecode(correct)};
            questions.AddRange(incorrect.Select(HttpUtility.HtmlDecode));
            questions.Shuffle();
            var letter = 'A';
            for (int i = 0; i < questions.Count; i++)
            {
                questions[i] = $"{letter++}. {questions[i]}";
            }
            return questions;
        }
    }
}