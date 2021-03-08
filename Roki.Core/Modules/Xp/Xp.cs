using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Xp.Common;
using Roki.Modules.Xp.Extensions;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;
using StackExchange.Redis;

namespace Roki.Modules.Xp
{
    public partial class Xp : RokiTopLevelModule
    {
        private readonly IConfigurationService _config;
        private readonly IHttpClientFactory _http;
        private readonly IRokiDbService _dbService;
        private readonly IDatabase _cache;

        public Xp(IHttpClientFactory http, IRokiDbService dbService, IRedisCache cache, IConfigurationService config)
        {
            _http = http;
            _dbService = dbService;
            _config = config;
            _cache = cache.Redis.GetDatabase();
        }
        
        [RokiCommand, Description, Usage, Aliases]
        public async Task Experience(IUser user = null)
        {
            if (user == null || user.IsBot)
            {
                user = Context.User;
            }

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            RedisValue image = await _cache.StringGetAsync($"xp:image:{user.Id}");
            if (image.HasValue)
            {
                await using var stream = new MemoryStream((byte[]) image);
                await Context.Channel.SendFileAsync(stream, $"xp-{user.Id}.png").ConfigureAwait(false);
                return;
            }

            Stream avatar;

            using (HttpClient http = _http.CreateClient())
            {
                try
                {
                    // handle gif avatars in future
                    string avatarUrl = user.GetAvatarUrl(ImageFormat.Png, 512) ?? user.GetDefaultAvatarUrl();
                    avatar = await http.GetStreamAsync(avatarUrl).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    Log.Error("Error downloading avatar");
                    return;
                }
            }

            var data = await _dbService.Context.UserData.AsNoTracking()
                .AsAsyncEnumerable()
                .Where(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id)
                .Select(x => new { x.Xp, x.LastLevelUp})
                .SingleAsync();
            
            var xp = new XpLevel(data.Xp);
            int rank = await _dbService.GetUserXpRankAsync(Context.User.Id, Context.Guild.Id);
            bool doubleXp = await _dbService.Context.Subscriptions.AsNoTracking()
                .Where(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id && x.Item.Details == "doublexp")
                .AnyAsync();
            bool fastXp = await _dbService.Context.Subscriptions.AsNoTracking()
                .Where(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id && x.Item.Details == "fastxp")
                .AnyAsync();

            await using MemoryStream xpImage = XpDrawExtensions.GenerateXpBar(avatar, xp, $"{rank}", 
                user.Username, user.Discriminator, data.LastLevelUp, doubleXp, fastXp);
            await _cache.StringSetAsync($"xp:image:{user.Id}", xpImage.ToArray(), TimeSpan.FromMinutes(5), flags: CommandFlags.FireAndForget);
            await Context.Channel.SendFileAsync(xpImage, $"xp-{user.Id}.png").ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task XpLeaderboard(int page = 0)
        {
            if (page < 0)
                return;
            if (page > 0)
                page -= 1;

            var list = await _dbService.Context.UserData.AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.GuildId == Context.Guild.Id)
                .OrderByDescending(x => x.Currency)
                .Skip(page * 9)
                .Take(9)
                .Select(x => new
                {
                    x.Xp,
                    x.User.Username,
                    x.User.Discriminator
                })
                .ToListAsync();
            
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context).WithTitle("XP Leaderboard");
            int i = 9 * page + 1;
            foreach (var data in list)
            {
                embed.AddField($"#{i++} {data.Username}#{data.Discriminator}", $"Level `{new XpLevel(data.Xp).Level:N0}` - `{data.Xp:N0}` xp");
            }

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task XpNotification(string notify = null, IUser user = null)
        {
            var caller = (IGuildUser) Context.User;
            var isAdmin = false;
            if (user != null)
            {
                if (!caller.GuildPermissions.Administrator)
                    return;
                
                isAdmin = true;
            }

            if (string.IsNullOrWhiteSpace(notify))
            {
                await Context.Channel.SendErrorAsync("Please provide notification location for xp: none, dm, server.")
                    .ConfigureAwait(false);
                return;
            }

            int location;
            if (notify.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                location = 0;
            }
            else if (notify.Equals("server", StringComparison.OrdinalIgnoreCase))
            {
                location = 1;
            }
            else if (notify.Equals("dm", StringComparison.OrdinalIgnoreCase))
            {
                location = 2;
            }
            else
            {
                await Context.Channel.SendErrorAsync("Not a valid option. Options are none, dm, or server.")
                    .ConfigureAwait(false);
                return;
            }

            // todo limit notif locations in guild config
            if (!isAdmin)
            {
                UserData data = await _dbService.Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id);
                data.NotificationLocation = location;
            }
            else
            {
                UserData data = await _dbService.Context.UserData.AsAsyncEnumerable().SingleAsync(x => x.UserId == user.Id && x.GuildId == Context.Guild.Id);
                data.NotificationLocation = location;
            }

            await _dbService.SaveChangesAsync();
            
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithDescription("Successfully changed xp notification preferences."))
                .ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task XpRewards(int page = 0)
        {
            if (page < 0)
            {
                page = 0;
            }

            if (page > 0)
            {
                page--;
            }

            List<XpReward> rewards = await _dbService.Context.XpRewards.AsAsyncEnumerable().Where(x => x.GuildId == Context.Guild.Id).OrderByDescending(x => x.Level).ToListAsync();
            if (rewards.Count == 0)
            {
                await Context.Channel.SendErrorAsync("There are currently no XP rewards setup for this server.").ConfigureAwait(false);
                return;
            }
            
            int totalPages = rewards.Count / 9;
            if (page > totalPages)
            {
                page = totalPages;
            }
            
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle("XP Rewards")
                .WithFooter($"Page {page + 1}/{totalPages + 1}");
            
            GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

            // todo format rewards embed
            embed.WithDescription(string.Join("\n", rewards
                .Skip(page * 9)
                .Take(9)
                .Select(r => r.Details.StartsWith("currency", StringComparison.Ordinal)
                    ? $"Level `{r.Level}` - `{int.Parse(r.Description):N0}` {guildConfig.CurrencyIcon}"
                    : $"Level `{r.Level}` - <@&{r.Description}>")));

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
    }
}