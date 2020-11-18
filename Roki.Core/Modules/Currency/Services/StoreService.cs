using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
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

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        public StoreService(DiscordSocketClient client, IMongoService mongo)
        {
            _client = client;
            _mongo = mongo;
            CheckSubscriptions();
        }

        private void CheckSubscriptions()
        {
            _timer = new Timer(RemoveSubEvent, null, TimeSpan.Zero, TimeSpan.FromHours(3));
        }

        private async void RemoveSubEvent(object state)
        {
            var expired = await _mongo.Context.RemoveExpiredSubscriptionsAsync().ConfigureAwait(false);
            foreach (var (userId, subs) in expired)
            {
                foreach (var sub in subs)
                {
                    var item = await _mongo.Context.GetStoreItemByObjectIdAsync(sub.GuildId, sub.Id).ConfigureAwait(false);
                    if (item.Category.Equals("Boost", StringComparison.Ordinal)) 
                        continue;
                    try
                    {
                        var guild = _client.GetGuild(sub.GuildId);
                        if (!(guild.GetUser(userId) is IGuildUser user)) 
                            continue;
                    
                        var role = user.GetRoles().FirstOrDefault(r => r.Name.Contains(item.Description, StringComparison.OrdinalIgnoreCase));
                        if (role == null) 
                            continue;
                    
                        await user.RemoveRoleAsync(role).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn(e, "Error while tyring to remove subscription role");
                    }
                }
            }
        }
    }
}