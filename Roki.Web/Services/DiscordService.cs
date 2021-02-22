using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Roki.Web.Models;

namespace Roki.Web.Services
{
    public class DiscordService
    {
        private const string ApiBaseUrl = "https://discord.com/api/v8/";
        
        private readonly IHttpClientFactory _factory;

        public DiscordService(IHttpClientFactory factory)
        {
            _factory = factory;
        }

        public async Task<List<DiscordGuild>> GetOwnerGuilds(string token)
        {
            using HttpClient http = _factory.CreateClient();
            http.BaseAddress = new Uri(ApiBaseUrl);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using Task<JsonDocument> json = JsonDocument.ParseAsync(await http.GetStreamAsync("users/@me/guilds"));

            var guilds = new List<DiscordGuild>();
            foreach (JsonElement element in json.Result.RootElement.EnumerateArray())
            {
                if (element.GetProperty("owner").GetBoolean() || (uint.Parse(element.GetProperty("permissions").GetString()!) & 0x20) == 0x20)
                {
                    guilds.Add(new DiscordGuild
                    {
                        Id = element.GetProperty("id").GetString(),
                        Name = element.GetProperty("name").GetString(),
                        Icon = element.GetProperty("icon").GetString()
                    });
                }
            }

            return guilds;
        }
    }
}