using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Roki.Core.Services;
using Roki.Modules.Games.Common;

namespace Roki.Modules.Games.Services
{
    public class TriviaService : IRService
    {
        private const string OpenTdbUrl = "https://opentdb.com/api.php";
        private readonly IHttpClientFactory _http;

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
    }
}