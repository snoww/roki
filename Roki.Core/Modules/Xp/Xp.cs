using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        private readonly RokiContext _context;
        private readonly IDatabase _cache;

        public Xp(IHttpClientFactory http, RokiContext context, IRedisCache cache, IConfigurationService config)
        {
            _http = http;
            _context = context;
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
            long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            RedisValue image = await _cache.StringGetAsync($"xp:image:{user.Id}:{Context.Guild.Id}");
            if (image.HasValue)
            {
                await using var stream = new MemoryStream((byte[]) image);
                await Context.Channel.SendFileAsync(stream, $"xp-{user.Username}-{seconds}.png").ConfigureAwait(false);
                return;
            }

            Stream avatar;

            using (HttpClient http = _http.CreateClient())
            {
                try
                {
                    // handle gif avatars in future
                    string avatarUrl = user.GetAvatarUrl(ImageFormat.Png, 256) ?? user.GetDefaultAvatarUrl();
                    avatar = await http.GetStreamAsync(avatarUrl).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    Log.Error("Error downloading avatar");
                    return;
                }
            }

            var data = await _context.UserData.AsNoTracking()
                .AsQueryable()
                .Where(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id)
                .Select(x => new { x.Xp, x.LastLevelUp, x.LastXpGain})
                .SingleAsync();
            
            var xp = new XpLevel(data.Xp);
            int rank = await GetUserXpRank(Context.User.Id, Context.Guild.Id);
            bool doubleXp = await _context.Subscriptions.AsNoTracking()
                .AnyAsync(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id && x.Item.Details == "doublexp");
            bool fastXp = await _context.Subscriptions.AsNoTracking()
                .AnyAsync(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id && x.Item.Details == "fastxp");

            await using MemoryStream xpImage = XpDrawExtensions.GenerateXpBar(avatar, xp, $"{rank}", 
                user.Username, user.Discriminator, data.LastLevelUp, doubleXp, fastXp);
            await avatar.DisposeAsync();
            await _cache.StringSetAsync($"xp:image:{user.Id}:{Context.Guild.Id}", xpImage.ToArray(), TimeSpan.FromMinutes(5), flags: CommandFlags.FireAndForget);
            await Context.Channel.SendFileAsync(xpImage, $"xp-{user.Id}-{seconds}.png").ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task XpLeaderboard(IUser user = null)
        {
            user ??= Context.User;
            var leaderboard = await _context.UserData.AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.GuildId == Context.Guild.Id && x.UserId != Roki.BotId)
                .OrderByDescending(x => x.Xp)
                .Take(9)
                .Select(x => new
                {
                    x.Xp,
                    x.User.Username,
                    x.User.Discriminator
                })
                .ToListAsync();
            
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle("XP Leaderboard")
                .WithFooter($"To show other pages: {await _config.GetGuildPrefix(Context.Guild.Id)}xplb <page>");

            if (leaderboard.Any(x => x.Username == user.Username && x.Discriminator == user.Discriminator))
            {
                int i = 1;
                foreach (var u in leaderboard)
                {
                    if (u.Username == user.Username && u.Discriminator == user.Discriminator)
                    {
                        embed.AddField($"#{i++} __{u.Username}#{u.Discriminator}__", $"Level `{new XpLevel(u.Xp).Level:N0}` - `{u.Xp:N0}` xp");
                    }
                    else
                    {
                        embed.AddField($"#{i++} {u.Username}#{u.Discriminator}", $"Level `{new XpLevel(u.Xp).Level:N0}` - `{u.Xp:N0}` xp");
                    }
                }
            }
            else
            {
                (long rank, string username, string discriminator, long xp) = await GetUserXpRankWithXp(user.Id, Context.Guild.Id);
                int i = 1;
                foreach (var u in leaderboard.Take(8))
                {
                    embed.AddField($"#{i++} {u.Username}#{u.Discriminator}", $"Level `{new XpLevel(u.Xp).Level:N0}` - `{u.Xp:N0}` xp");
                }
                embed.AddField($"#{rank} __{username}#{discriminator}__", $"Level `{new XpLevel(xp).Level:N0}` - `{xp:N0}` xp");
            }
            
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task XpLeaderboard(int page)
        {
            if (page < 0)
                return;
            if (page > 0)
                page -= 1;

            var leaderboard = await _context.UserData.AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.GuildId == Context.Guild.Id && x.UserId != Roki.BotId)
                .OrderByDescending(x => x.Xp)
                .Skip(page * 9)
                .Take(9)
                .Select(x => new
                {
                    x.Xp,
                    x.User.Username,
                    x.User.Discriminator
                })
                .ToListAsync();
            
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle("XP Leaderboard")
                .WithFooter($"Page {page+1} | To show other pages: {await _config.GetGuildPrefix(Context.Guild.Id)}xplb <page>");
            int i = 9 * page + 1;
            foreach (var data in leaderboard)
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
                UserData data = await _context.UserData.AsQueryable().SingleAsync(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id);
                data.NotificationLocation = location;
            }
            else
            {
                UserData data = await _context.UserData.AsQueryable().SingleAsync(x => x.UserId == user.Id && x.GuildId == Context.Guild.Id);
                data.NotificationLocation = location;
            }

            await _context.SaveChangesAsync();
            
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

            List<XpReward> rewards = await _context.XpRewards.AsQueryable().Where(x => x.GuildId == Context.Guild.Id).OrderByDescending(x => x.Level).ToListAsync();
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

        private async Task<int> GetUserXpRank(ulong userId, ulong guildId)
        {
            var conn = (NpgsqlConnection) _context.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("select r.rank from (select d.uid, d.xp, rank() over (order by d.xp desc) rank from user_data d where d.guild_id = (@guild_id)) r where r.uid = (@uid)", conn);
            cmd.Parameters.AddWithValue("guild_id", (long) guildId);
            cmd.Parameters.AddWithValue("uid", (long) userId);
            await cmd.PrepareAsync();
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            return reader.GetInt32(0);
        }
        
        private async Task<(long, string, string, long)> GetUserXpRankWithXp(ulong userId, ulong guildId)
        {
            var conn = (NpgsqlConnection) _context.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                select r.rank, r.username, r.discriminator, r.xp
                from (select u.username, u.discriminator, ud.uid, ud.xp, rank() over (order by ud.xp desc) rank
                      from user_data ud
                               inner join users as u on ud.uid = u.id
                      where ud.guild_id = (@guild_id)) r
                where r.uid = (@uid);", conn);
            cmd.Parameters.AddWithValue("guild_id", (long) guildId);
            cmd.Parameters.AddWithValue("uid", (long) userId);
            await cmd.PrepareAsync();
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            // rank | username | discriminator | xp
            return (reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3));
        }
    }
}