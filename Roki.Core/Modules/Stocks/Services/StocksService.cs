using System.Net.Http;
using System.Text.Json;
using Roki.Core.Services;

namespace Roki.Modules.Stocks.Services
{
    public class StocksService : IRService
    {
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _httpFactory;
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions{PropertyNameCaseInsensitive = true};

        public StocksService(IRokiConfig config, IHttpClientFactory httpFactory)
        {
            _config = config;
            _httpFactory = httpFactory;
        }
    }
}