using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MongoDB.Bson;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Moderation.Services
{
    public class PowersService : IRokiService
    {
        private readonly IMongoService _mongo;
        private readonly ConcurrentDictionary<ulong, DateTime> _muted = new ConcurrentDictionary<ulong, DateTime>();
        private readonly ConcurrentDictionary<ulong, DateTime> _blocked = new ConcurrentDictionary<ulong, DateTime>();
        private readonly ConcurrentDictionary<ulong, DateTime> _timeout = new ConcurrentDictionary<ulong, DateTime>();
        

        public PowersService(IMongoService mongo)
        {
            _mongo = mongo;
        }

        public async Task<bool> ConsumePower(ulong guildId, ulong userId, ObjectId power)
        {
            var inv = (await _mongo.Context.GetUserAsync(userId).ConfigureAwait(false)).Inventory;
            foreach (var item in inv)
            {
                var listing = await _mongo.Context.GetStoreItemByIdAsync(guildId, item.Id).ConfigureAwait(false);
                if (listing.Id != power) continue;
                
                await _mongo.Context.AddOrUpdateUserInventoryAsync(userId, guildId, power, -1).ConfigureAwait(false);
                return true;
            }

            return false;
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
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithTitle("🔈 Unmute").WithDescription($"{user.Mention} has been unmuted from voice"))
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
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithTitle("🔈 Unblock").WithDescription($"{user.Mention} has been unblocked from chat"))
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
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(ctx).WithTitle("🔈 Unban").WithDescription($"{user.Mention} has been unbanned from voice and chat"))
                    .ConfigureAwait(false);
            }
        }
    }
}