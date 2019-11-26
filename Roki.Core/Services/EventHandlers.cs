using System.Threading.Tasks;
using Discord.WebSocket;

namespace Roki.Core.Services
{
    public class EventHandlers
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;

        public EventHandlers(DbService db, DiscordSocketClient client)
        {
            _db = db;
            _client = client;
            _client.UserJoined += UserJoined;
            _client.UserUpdated += UserUpdated;
        }

        private Task UserUpdated(SocketUser before, SocketUser after)
        {
            throw new System.NotImplementedException();
        }

        private Task UserJoined(SocketGuildUser user)
        {
            using var uow = _db.GetDbContext();
//            uow.Users
        }
    }
}