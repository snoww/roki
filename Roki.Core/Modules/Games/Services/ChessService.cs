using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using Roki.Modules.Games.Common;
using Roki.Services;

namespace Roki.Modules.Games.Services
{
    public class ChessService : IRokiService
    {
        private readonly IHttpClientFactory _http;
        private const string LichessApi = "https://lichess.org/api/";

        public ChessService(IHttpClientFactory http)
        {
            _http = http;
        }

        public async Task<string> CreateChessChallenge(ChessArgs args)
        {
            using var http = _http.CreateClient();
            http.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded");
            var opts = new Dictionary<string, string>
            {
                {"clock.limit", args.Time.ToString()},
                {"clock.increment", args.Increment.ToString()}
            };
            
            var response = await http.PostAsync(LichessApi + "challenge/open", new FormUrlEncodedContent(opts)).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var json = JsonDocument.Parse(raw);
            return json.RootElement.GetProperty("challenge").GetProperty("url").GetString();
        }
    }
}