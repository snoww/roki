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
        private readonly IHttpClientFactory _http;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private static readonly Dictionary<string, string> Sources = File.ReadAllText("./data/nsfw.json").Deserialize<Dictionary<string, string>>();
        private const string BaseUrl = "https://www.reddit.com/r/{0}/random.json";

        public NsfwService(IHttpClientFactory http)
        {
            _http = http;
        }

        public async Task<string> GetRandomNsfw(NsfwCategory category)
        {
            var options = Sources[category.ToString().ToLower()].Split();
            var rng = new Random().Next(options.Length);

            try
            {
                using var http = _http.CreateClient();
                var result = await http.GetStringAsync(string.Format(BaseUrl, options[rng])).ConfigureAwait(false);
                using var json = JsonDocument.Parse(result);
                // maybe return source as well?
                return json.RootElement[0].GetProperty("data").GetProperty("children")[0].GetProperty("data").GetProperty("url").GetString();
            }
            catch (Exception e)
            {
                Logger.Warn(e, "Failed to get random image from: 'r/{subreddit}'", options[rng]);
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