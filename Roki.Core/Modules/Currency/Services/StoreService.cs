using System;
using System.Linq;
using System.Threading;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database;

namespace Roki.Modules.Currency.Services
{
    public class StoreService : IRokiService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly DiscordSocketClient _client;
        private Timer _timer;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        public StoreService(DiscordSocketClient client, IServiceScopeFactory scopeFactory)
        {
            _client = client;
            _scopeFactory = scopeFactory;
            CheckSubscriptions();
        }

        private void CheckSubscriptions()
        {           
            DateTime current = DateTime.UtcNow;
            TimeSpan timeToGo = new TimeSpan(24, 0, 0) - current.TimeOfDay;
            if (timeToGo < TimeSpan.Zero)
            {
                //time already passed
                return;
            }
            
            _timer = new Timer(RemoveSubEvent, null, timeToGo, Timeout.InfiniteTimeSpan);
        }

        private async void RemoveSubEvent(object state)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RokiContext>();
            DateTime now = DateTime.UtcNow.Date;
            if (!await context.Subscriptions.AsNoTracking().AnyAsync(x => x.Expiry <= now))
            {
                return;
            }
            
            foreach (var expired in context.Subscriptions.AsNoTracking().Include(x => x.Item)
                .Where(x => x.Expiry <= now)
                .Select(x => new { x.UserId, x.GuildId, x.Item.Details }))
            {
                string[] details = expired.Details.Split(';');
                if (details[0].Equals("boost", StringComparison.Ordinal))
                {
                    continue;
                }

                if (details[0].Equals("role", StringComparison.Ordinal))
                {
                    try
                    {
                        IGuildUser user = _client.GetGuild(expired.GuildId)?.GetUser(expired.UserId);
                        IRole role = user?.GetRoles().FirstOrDefault(r => r.Id.ToString() == details[1]);
                        if (role == null)
                        {
                            continue;
                        }

                        await user.RemoveRoleAsync(role);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn(e, "Error while tyring to remove subscription role <@&{roleid}>", details[1]);
                    }
                }
            }

            int rows = await context.Database.ExecuteSqlRawAsync("delete from subscription where expiry < current_date");
            Logger.Info("Removed {rows} expired subscription(s)", rows);
        }
    }
}