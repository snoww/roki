using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MongoDB.Bson;
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
            Dictionary<ulong, Dictionary<string, List<string>>> expired = await _mongo.Context.RemoveExpiredSubscriptionsAsync().ConfigureAwait(false);
            foreach ((ulong userId, Dictionary<string, List<string>> subs) in expired)
            {
                foreach ((string guildId, List<string> sub) in subs)
                {
                    foreach (string id in sub)
                    {
                        (_, Listing listing) = await _mongo.Context.GetStoreItemByObjectIdAsync(ulong.Parse(guildId), id).ConfigureAwait(false);
                        if (listing.Category.Equals("Boost", StringComparison.Ordinal)) 
                            continue;
                        try
                        {
                            SocketGuild guild = _client.GetGuild(ulong.Parse(guildId));
                            if (!(guild.GetUser(userId) is IGuildUser user)) 
                                continue;
                    
                            IRole role = user.GetRoles().FirstOrDefault(r => r.Name.Contains(listing.Description, StringComparison.OrdinalIgnoreCase));
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
}