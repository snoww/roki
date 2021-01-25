using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NLog;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Nsfw.Services
{
    public class NsfwService : IRokiService
    {
        private readonly IHttpClientFactory _factory;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Random Random = new();
        
        private static readonly Dictionary<string, List<string>> Sources = File.ReadAllText("./data/nsfw_sources.json").Deserialize<Dictionary<string, List<string>>>();
        private const string BaseUrl = "https://www.reddit.com/r/{0}/random.json";

        public NsfwService(IHttpClientFactory factory)
        {
            _factory = factory;
        }

        public async Task<string> GetRandomNsfw(NsfwCategory category)
        {
            List<string> options = Sources[category.ToString().ToLowerInvariant()];
            int rng = Random.Next(options.Count);

            try
            {
                using HttpClient http = _factory.CreateClient();
                string result = await http.GetStringAsync(string.Format(BaseUrl, options[rng])).ConfigureAwait(false);
                using JsonDocument json = JsonDocument.Parse(result);
                // maybe return source as well?
                return json.RootElement[0].GetProperty("data").GetProperty("children")[0].GetProperty("data").GetProperty("url").GetString();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to get random image from: r/{Subreddit}", options[rng]);
                return null;
            }
        }
    }

    public enum NsfwCategory
    {
        General,
        Ass,
        Boobs,
        Cosplay,
        Hentai,
        Mild,
        Gifs,
        Trans
    }
}