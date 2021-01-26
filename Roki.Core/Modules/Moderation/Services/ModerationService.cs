using System.Threading.Tasks;
using Discord;
using MongoDB.Driver;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Moderation.Services
{
    public class ModerationService : IRokiService
    {
        private readonly IMongoService _mongo;

        public ModerationService(IMongoService mongo)
        {
            _mongo = mongo;
        }

        public bool IsLoggingEnabled(ITextChannel channel)
        {
            return _mongo.Context.IsLoggingEnabled(channel);
        }

        public async Task ChangeChannelLoggingAsync(ulong channelId, bool enable)
        {
            UpdateDefinition<Channel> update = Builders<Channel>.Update.Set(x => x.Logging, enable);
            await _mongo.Context.ChangeChannelProperty(x => x.Id == channelId, update).ConfigureAwait(false);
        }
    }
}