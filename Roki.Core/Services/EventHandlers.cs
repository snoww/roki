using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

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
            _client.JoinedGuild += JoinedGuild;
        }

        private Task JoinedGuild(SocketGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                await guild.DownloadUsersAsync().ConfigureAwait(false);
                var users = guild.Users;
                foreach (var user in users)
                {
                    await uow.Users.GetOrCreateUserAsync(user);
                }
            });
            return Task.CompletedTask;
        }

        private Task UserUpdated(SocketUser before, SocketUser after)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                var user = await uow.Context.Users.FirstAsync(u => u.UserId == before.Id).ConfigureAwait(false);
                user.Username = after.Username;
                user.Discriminator = after.Discriminator;
                user.AvatarId = after.AvatarId;
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task UserJoined(SocketGuildUser user)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                await uow.Users.GetOrCreateUserAsync(user).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }
    }
}