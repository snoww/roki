using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Currency.Services
{
    public class StoreService : IRokiService
    {
        private readonly IMongoService _mongo;
        private readonly DiscordSocketClient _client;
        private Timer _timer;

        public StoreService(DiscordSocketClient client, IMongoService mongo)
        {
            _client = client;
            _mongo = mongo;
            CheckSubscriptions();
        }

        private void CheckSubscriptions()
        {
            _timer = new Timer(RemoveSubEvent, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        }

        private async void RemoveSubEvent(object state)
        {
            using var uow = _db.GetDbContext();
            var expired = uow.Subscriptions.GetExpiredSubscriptions();
            foreach (var sub in expired)
            {
                await uow.Subscriptions.RemoveSubscriptionAsync(sub.Id).ConfigureAwait(false);
                if (sub.Type.Equals("BOOST", StringComparison.OrdinalIgnoreCase)) continue;
                var guild = _client.GetGuild(sub.GuildId);
                if (!(guild.GetUser(sub.UserId) is IGuildUser user)) continue;
                var role = user.GetRoles().FirstOrDefault(r => r.Name.Contains(sub.Description, StringComparison.OrdinalIgnoreCase));
                if (role == null) continue;
                await user.RemoveRoleAsync(role).ConfigureAwait(false);
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}