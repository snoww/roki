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
        private readonly ConcurrentDictionary<ulong, DateTime> _muted = new ConcurrentDictionary<ulong, DateTime>();
        private readonly ConcurrentDictionary<ulong, DateTime> _blocked = new ConcurrentDictionary<ulong, DateTime>();
        private readonly ConcurrentDictionary<ulong, DateTime> _timeout = new ConcurrentDictionary<ulong, DateTime>();
        

        public PowersService(DbService db)
        {
            _db = db;
        }

        public async Task<bool> AvailablePower(ulong userId, string power)
        {
            using var uow = _db.GetDbContext();
            var inv =  await uow.Users.GetUserInventory(userId).ConfigureAwait(false);
            return inv.Any(i => i.Name.Equals(power, StringComparison.OrdinalIgnoreCase));
        }
        
        public async Task ConsumePower(ulong userId, string power)
        {
            using var uow = _db.GetDbContext();
            await uow.Users.UpdateUserInventory(userId, power, -1).ConfigureAwait(false);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task MuteUser(ICommandContext ctx, IGuildUser user)
        {
            var muted = ctx.Guild.Roles.First(r => r.Name.Contains("mute", StringComparison.OrdinalIgnoreCase));
            var time = DateTime.UtcNow + TimeSpan.FromMinutes(5);
            var updatedTime = time;
            _muted.AddOrUpdate(user.Id, time, (key, oldTime) => updatedTime += TimeSpan.FromMinutes(5));
            await user.AddRoleAsync(muted).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithTitle($"🔇 {ctx.User.Username} used a Mute Power on {user.Username}")
                    .WithDescription($"{user.Mention} is muted from voice for 5 minutes.\nUnmuted in {updatedTime - DateTime.UtcNow:m\\:ss}"))
                .ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromMinutes(5));
            _muted.TryGetValue(user.Id, out var latest);

            if (DateTime.UtcNow >= latest)
            {
                _muted.TryRemove(user.Id, out _);
                await user.RemoveRoleAsync(muted).ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle("🔈 Unmute").WithDescription($"{user.Mention} has been unmuted from voice"))
                    .ConfigureAwait(false);
            }
        }

        public async Task BlockUser(ICommandContext ctx, IGuildUser user)
        {
            var blocked = ctx.Guild.Roles.First(r => r.Name.Contains("blocked", StringComparison.OrdinalIgnoreCase));
            var time = DateTime.UtcNow + TimeSpan.FromMinutes(5);
            var updatedTime = time;
            _blocked.AddOrUpdate(user.Id, time, (key, oldTime) => updatedTime += TimeSpan.FromMinutes(5));
            await user.AddRoleAsync(blocked).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithTitle($"🔇 {ctx.User.Username} used a Block Power on {user.Username}")
                    .WithDescription($"{user.Mention} is blocked from chat for 5 minutes.\nUnblocked in {updatedTime - DateTime.UtcNow:m\\:ss}"))
                .ConfigureAwait(false);
            
            await Task.Delay(TimeSpan.FromMinutes(5));
            _blocked.TryGetValue(user.Id, out var latest);

            if (DateTime.UtcNow >= latest)
            {
                _blocked.TryRemove(user.Id, out _);
                await user.RemoveRoleAsync(blocked).ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle("🔈 Unblock").WithDescription($"{user.Mention} has been unblocked from chat"))
                    .ConfigureAwait(false);
            }
        }
        
        public async Task TimeoutUser(ICommandContext ctx, IGuildUser user)
        {
            var timeout = ctx.Guild.Roles.First(r => r.Name.Contains("timeout", StringComparison.OrdinalIgnoreCase));
            var time = DateTime.UtcNow + TimeSpan.FromMinutes(5);
            var updatedTime = time;
            _timeout.AddOrUpdate(user.Id, time, (key, oldTime) => updatedTime += TimeSpan.FromMinutes(5));
            await user.AddRoleAsync(timeout).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithTitle($"🔇 {ctx.User.Username} used a Timeout Power on {user.Username}")
                    .WithDescription($"{user.Mention} is timed out for 5 minutes.\nUnbanned in {updatedTime - DateTime.UtcNow:m\\:ss}"))
                .ConfigureAwait(false);
            
            await Task.Delay(TimeSpan.FromMinutes(5));
            _timeout.TryGetValue(user.Id, out var latest);

            if (DateTime.UtcNow >= latest)
            {
                _timeout.TryRemove(user.Id, out _);
                await user.RemoveRoleAsync(timeout).ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle("🔈 Unban").WithDescription($"{user.Mention} has been unbanned from voice and chat"))
                    .ConfigureAwait(false);
            }
        }
    }
}