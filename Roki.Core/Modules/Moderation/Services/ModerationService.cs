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
    }
}