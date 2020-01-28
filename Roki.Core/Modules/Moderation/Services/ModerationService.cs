using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services;
using Roki.Services;

namespace Roki.Modules.Moderation.Services
{
    public class ModerationService : IRokiService
    {
        private readonly DbService _db;

        public ModerationService(DbService db)
        {
            _db = db;
        }

        public async Task LoggingChannel(ulong channelId, bool enable)
        {
            using var uow = _db.GetDbContext();
            var channel = await uow.Context.Channels.FirstAsync(c => c.ChannelId == channelId);
            channel.Logging = enable;
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}