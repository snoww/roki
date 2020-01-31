using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
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
        private readonly Logger _log;
        
        private static readonly Dictionary<string, string> Sources = File.ReadAllText("./data/nsfw.json").Deserialize<Dictionary<string, string>>();
        private const string BaseUrl = "https://www.reddit.com/r/{0}/random.json";
        private const string GfycatUrl = "https://thumbs.gfycat/{0}-mobile.mp4";

        public NsfwService(IHttpClientFactory http)
        {
            _http = http;
            _log = LogManager.GetCurrentClassLogger();
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
                var url = json.RootElement[0].GetProperty("data").GetProperty("children")[0].GetProperty("data").GetProperty("url").GetString();
                string path;

                if (url.Contains("gfycat", StringComparison.OrdinalIgnoreCase))
                {
                    url = string.Format(GfycatUrl, Path.GetFileName(url));
                    path = $"./temp/{Path.GetFileName(url)}";
                }
                else
                {
                    path = $"./temp/{Path.GetFileName(url)}";
                }
                
                using var client = new WebClient();
                await client.DownloadFileTaskAsync(url, path).ConfigureAwait(false);
                return path;
            }
            catch (Exception e)
            {
                _log.Warn(e, "Failed to get random image from: 'r/{0}'", options[rng]);
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