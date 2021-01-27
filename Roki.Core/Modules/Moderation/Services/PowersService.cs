using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MongoDB.Bson;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Moderation.Services
{
    public class PowersService : IRokiService
    {
        private readonly ConcurrentDictionary<ulong, DateTime> _blocked = new();
        private readonly IMongoService _mongo;
        private readonly ConcurrentDictionary<ulong, DateTime> _muted = new();
        private readonly ConcurrentDictionary<ulong, DateTime> _timeout = new();


        public PowersService(IMongoService mongo)
        {
            _mongo = mongo;
        }

        public async Task<bool> ConsumePower(IUser user, ulong guildId, ObjectId power)
        {
            Dictionary<ObjectId, Item> inv = (await _mongo.Context.GetOrAddUserAsync(user, guildId.ToString()).ConfigureAwait(false)).Data[guildId.ToString()].Inventory;
            foreach ((ObjectId id, Item _) in inv)
            {
                (ObjectId listingId, _) = await _mongo.Context.GetStoreItemByObjectIdAsync(guildId, id).ConfigureAwait(false);
                if (listingId != power) continue;

                await _mongo.Context.AddOrUpdateUserInventoryAsync(user, guildId.ToString(), power, -1).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        public async Task MuteUser(ICommandContext ctx, IGuildUser user)
        {
            IRole muted = ctx.Guild.Roles.First(r => r.Name.Contains("mute", StringComparison.OrdinalIgnoreCase));
            DateTime time = DateTime.UtcNow + TimeSpan.FromMinutes(5);
            DateTime updatedTime = time;
            _muted.AddOrUpdate(user.Id, time, (key, oldTime) => updatedTime += TimeSpan.FromMinutes(5));
            await user.AddRoleAsync(muted).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithTitle($"🔇 {ctx.User.Username} used a Mute Power on {user.Username}")
                    .WithDescription($"{user.Mention} is muted from voice for 5 minutes.\nUnmuted in {updatedTime - DateTime.UtcNow:m\\:ss}"))
                .ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromMinutes(5));
            _muted.TryGetValue(user.Id, out DateTime latest);

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
            IRole blocked = ctx.Guild.Roles.First(r => r.Name.Contains("blocked", StringComparison.OrdinalIgnoreCase));
            DateTime time = DateTime.UtcNow + TimeSpan.FromMinutes(5);
            DateTime updatedTime = time;
            _blocked.AddOrUpdate(user.Id, time, (key, oldTime) => updatedTime += TimeSpan.FromMinutes(5));
            await user.AddRoleAsync(blocked).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithTitle($"🔇 {ctx.User.Username} used a Block Power on {user.Username}")
                    .WithDescription($"{user.Mention} is blocked from chat for 5 minutes.\nUnblocked in {updatedTime - DateTime.UtcNow:m\\:ss}"))
                .ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromMinutes(5));
            _blocked.TryGetValue(user.Id, out DateTime latest);

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
            IRole timeout = ctx.Guild.Roles.First(r => r.Name.Contains("timeout", StringComparison.OrdinalIgnoreCase));
            DateTime time = DateTime.UtcNow + TimeSpan.FromMinutes(5);
            DateTime updatedTime = time;
            _timeout.AddOrUpdate(user.Id, time, (key, oldTime) => updatedTime += TimeSpan.FromMinutes(5));
            await user.AddRoleAsync(timeout).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithTitle($"🔇 {ctx.User.Username} used a Timeout Power on {user.Username}")
                    .WithDescription($"{user.Mention} is timed out for 5 minutes.\nUnbanned in {updatedTime - DateTime.UtcNow:m\\:ss}"))
                .ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromMinutes(5));
            _timeout.TryGetValue(user.Id, out DateTime latest);

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