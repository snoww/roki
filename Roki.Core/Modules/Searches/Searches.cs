using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extentions;

namespace Roki.Modules.Searches
{
    public partial class Searches : RokiTopLevelModule
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IGoogleApiService _google;

        public Searches(IConfiguration config, IGoogleApiService google, IHttpClientFactory httpFactory)
        {
            _config = config;
            _google = google;
            _httpFactory = httpFactory;
        }

        public async Task Time([Leftover] string query)
        {
            
        }

        public async Task<bool> ValidateQuery(IMessageChannel channel, string query)
        {
            if (!string.IsNullOrWhiteSpace(query))
                return true;
            
            var embed = new EmbedBuilder().WithErrorColor()
                .WithDescription("No search query provided");
            await channel.EmbedAsync(embed);
            return false;
        }

    }
}