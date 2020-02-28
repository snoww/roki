using System.Threading.Tasks;
using Roki.Services;

namespace Roki.Modules.Moderation.Services
{
    public class ModerationService : IRokiService
    {
        private readonly IMongoService _mongo;

        public ModerationService(IMongoService mongo)
        {
            _mongo = mongo;
        }

        public async Task LoggingChannel(ulong channelId, bool enable)
        {
            await _mongo.Context.ChangeChannelLogging(channelId, enable).ConfigureAwait(false);
        }
    }
}