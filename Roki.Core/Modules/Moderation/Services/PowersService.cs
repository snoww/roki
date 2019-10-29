using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using Roki.Core.Services;
using Roki.Extensions;

namespace Roki.Modules.Moderation.Services
{
    public class PowersService : IRService
    {
        private readonly DbService _db;
        public ConcurrentDictionary<ulong, DateTime> Muted = new ConcurrentDictionary<ulong, DateTime>();
        public ConcurrentDictionary<ulong, DateTime> Blocked = new ConcurrentDictionary<ulong, DateTime>();
        public ConcurrentDictionary<ulong, DateTime> Timeout = new ConcurrentDictionary<ulong, DateTime>();
        

        public PowersService(DbService db)
        {
            _db = db;
        }

        public async Task<bool> AvailablePower(ulong userId, string power)
        {
            using (var uow = _db.GetDbContext())
            {
                var inv =  await uow.DUsers.GetOrCreateUserInventory(userId).ConfigureAwait(false);
                var invJson = (JObject) JToken.FromObject(inv);
                return invJson[power].Value<int>() > 0;
            }
        }
        
        public async Task ConsumePower(ulong userId, string power)
        {
            using (var uow = _db.GetDbContext())
            {
                await uow.DUsers.UpdateUserInventory(userId, power, -1).ConfigureAwait(false);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task MuteUser(ICommandContext ctx, IGuildUser user)
        {
            var muted = ctx.Guild.Roles.First(r => r.Name.Contains("mute", StringComparison.OrdinalIgnoreCase));
            var time = DateTime.UtcNow + TimeSpan.FromMinutes(5);
            var updatedTime = time;
            Muted.AddOrUpdate(user.Id, time, (key, oldTime) => updatedTime += TimeSpan.FromMinutes(5));
            await user.AddRoleAsync(muted).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithTitle($"🔇 {ctx.User.Username} used a Mute Power on {user.Username}")
                    .WithDescription($"{user.Mention} is muted for 5 minutes.\nUnmuted in {updatedTime - DateTime.UtcNow:m\\:ss}"))
                .ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromMinutes(5));
            Muted.TryGetValue(user.Id, out var latest);

            if (DateTime.UtcNow >= latest)
            {
                await user.RemoveRoleAsync(muted).ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle("🔈 Unmute").WithDescription($"{user.Mention} has been unmuted"))
                    .ConfigureAwait(false);
            }
        }
    }
}