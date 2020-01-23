using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Xp.Common;
using Roki.Modules.Xp.Extensions;

namespace Roki.Modules.Xp
{
    public partial class Xp : RokiTopLevelModule
    {
        private readonly DbService _db;
        private readonly IHttpClientFactory _http;

        public Xp(DbService db, IHttpClientFactory http)
        {
            _db = db;
            _http = http;
        }
        
        [RokiCommand, Description, Usage, Aliases]
        public async Task Experience(IUser user = null)
        {
            if (user == null || user.IsBot)
            {
                user = ctx.User;
            }

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            
            Stream avatar;

            using (var http = _http.CreateClient())
            {
                try
                {
                    // handle gif avatars in future
                    var avatarUrl = user.GetAvatarUrl(ImageFormat.Png, 512) ?? user.GetDefaultAvatarUrl();
                    avatar = await http.GetStreamAsync(avatarUrl).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    _log.Error("Error downloading avatar");
                    return;
                }
            }

            using var uow = _db.GetDbContext();
            var dUser = await uow.Users.GetUserAsync(user.Id).ConfigureAwait(false);
            var xp = new XpLevel(dUser.TotalXp);
            var rank = uow.Users.GetUserXpRank(user.Id);
            
            // use cache later
            await using var xpImage = XpDrawExtensions.GenerateXpBar(avatar, 
                xp.ProgressXp, xp.RequiredXp, $"{xp.TotalXp}", $"{xp.Level}", $"{rank}", 
                user.Username, user.Discriminator, dUser.LastLevelUp);
            await ctx.Channel.SendFileAsync(xpImage, $"xp-{user.Id}.png").ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task XpLeaderboard(int page = 0)
        {
            if (page < 0)
                return;
            if (page > 0)
                page -= 1;
            using var uow = _db.GetDbContext();
            var list = uow.Users.GetUsersXpLeaderboard(page);
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle("XP Leaderboard");
            var i = 9 * page + 1;
            foreach (var user in list)
            {
                embed.AddField($"#{i++} {user.Username}#{user.Discriminator}", $"Level `{new XpLevel(user.TotalXp).Level:N0}` - `{user.TotalXp:N0}` xp");
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task XpNotification(string notify = null, IUser user = null)
        {
            var caller = (IGuildUser) ctx.User;
            var isAdmin = false;
            if (user != null)
            {
                if (!caller.GuildPermissions.Administrator)
                    return;
                
                isAdmin = true;
            }

            if (string.IsNullOrWhiteSpace(notify))
            {
                await ctx.Channel.SendErrorAsync("Please provide notification location for xp: none, dm, server.")
                    .ConfigureAwait(false);
                return;
            }

            if (!notify.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                !notify.Equals("dm", StringComparison.OrdinalIgnoreCase) ||
                !notify.Equals("server", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.Channel.SendErrorAsync("Not a valid option. Options are none, dm, or server.")
                    .ConfigureAwait(false);
                return;
            }

            using var uow = _db.GetDbContext();
            if (!isAdmin)
            {
                await uow.Users.ChangeNotificationLocation(ctx.User.Id, notify).ConfigureAwait(false);
            }
            else
            {
                await uow.Users.ChangeNotificationLocation(user.Id, notify).ConfigureAwait(false);
            }
            
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithDescription("Successfully changed xp notification preferences."))
                .ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task XpRewards(int page = 0, bool id = false)
        {
            if (page < 0)
            {
                page = 0;
            }

            if (page > 0)
            {
                page--;
            }
            
            using var uow = _db.GetDbContext();
            var rewards = await uow.Guilds.GetAllXpRewardsAsync(ctx.Guild.Id).ConfigureAwait(false);
            if (rewards == null || rewards.Count == 0)
            {
                await ctx.Channel.SendErrorAsync("There are currently no XP rewards setup for this server.").ConfigureAwait(false);
                return;
            }

            var sorted = rewards.OrderByDescending(r => r.XpLevel).ToList();
            
            var totalPages = sorted.Count / 9;
            if (page > totalPages)
            {
                page = totalPages;
            }
            
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle("XP Rewards")
                .WithFooter($"Page {page + 1}/{totalPages + 1}");

            if (!id)
            {
                embed.WithDescription(string.Join("\n", sorted
                    .Skip(page * 9)
                    .Take(9)
                    .Select(r => r.Type == "currency"
                        ? $"Level `{r.XpLevel}` - `{int.Parse(r.Reward):N0}` {Roki.Properties.CurrencyIcon}"
                        : $"Level `{r.XpLevel}` - <@&{r.Reward}>")));
            }
            else
            {
                embed.WithDescription(string.Join("\n", sorted
                    .Skip(page * 9)
                    .Take(9)
                    .Select(r => r.Type == "currency"
                        ? $"`{r.Id}` Level `{r.XpLevel}` - `{int.Parse(r.Reward):N0}` {Roki.Properties.CurrencyIcon}"
                        : $"`{r.Id}` Level `{r.XpLevel}` - <@&{r.Reward}>")));
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task XpRewardAdd(RewardType rewardType, int level, string reward)
        {
            using var uow = _db.GetDbContext();
            if (rewardType == RewardType.Currency)
            {
                if (!int.TryParse(reward, out var rewardAmount))
                {
                    await ctx.Channel.SendErrorAsync("Make sure you enter a valid number for the currency reward.").ConfigureAwait(false);
                    return;
                }

                
                var currReward = await uow.Guilds.AddXpRewardAsync(ctx.Guild.Id, level, "currency", rewardAmount.ToString());
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("XP Reward Added")
                        .WithDescription("Successfully added a new XP reward.\n" +
                                         $"Reward ID: `{currReward.Id}`\n" +
                                         $"XP Level: `{level}`\n" +
                                         "Reward Type: currency\n" +
                                         $"Reward Amount: `{rewardAmount:N0}`"))
                    .ConfigureAwait(false);
                
                return;
            }

            var roleId = ctx.Message.MentionedRoleIds.FirstOrDefault();
            if (roleId == 0)
            {
                if (!ulong.TryParse(reward, out roleId))
                {
                    await ctx.Channel.SendErrorAsync("Please specify a role for the XP role reward.\nIf the role is not mentionable, use the role ID instead")
                        .ConfigureAwait(false);
                    return;
                }
            }

            var role = ctx.Guild.GetRole(roleId);
            if (role == null)
            {
                await ctx.Channel.SendErrorAsync("Could not find that role. Please check the role ID again.").ConfigureAwait(false);
                return;
            }
            
            var roleReward = await uow.Guilds.AddXpRewardAsync(ctx.Guild.Id, level, "role", role.Id.ToString());
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle("XP Reward Added")
                    .WithDescription("Successfully added a new XP reward.\n" +
                                     $"Reward ID: `{roleReward.Id}`\n" +
                                     $"XP Level: `{level}`\n" +
                                     "Reward Type: role\n" +
                                     $"Reward Role: <@&{role.Id}>"))
                .ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task XpRewardRemove(string id = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                await ctx.Channel.SendErrorAsync("Please specify the XP reward ID. You can obtain the IDs by using `xpr <page_num> true`.")
                    .ConfigureAwait(false);
                return;
            }

            using var uow = _db.GetDbContext();
            var success = await uow.Guilds.RemoveXpRewardAsync(ctx.Guild.Id, id).ConfigureAwait(false);
            if (success)
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("XP Reward Removed.")
                        .WithDescription($"Successfully removed reward with ID: `{id}`"))
                    .ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.SendErrorAsync($"Something went wrong when trying to remove the reward with ID: `{id}`\n" +
                                                 $"Please double check the ID and try again.")
                    .ConfigureAwait(false);
            }
        }
        
        public enum RewardType
        {
            Role,
            Currency,
        }
    }
}