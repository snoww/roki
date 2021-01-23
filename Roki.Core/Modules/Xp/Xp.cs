using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MongoDB.Bson;
using MongoDB.Driver;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Xp.Common;
using Roki.Modules.Xp.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Xp
{
    public partial class Xp : RokiTopLevelModule
    {
        private readonly IMongoService _mongo;
        private readonly IHttpClientFactory _http;
        
        // hard-coding xp boosts
        // needs fixing in future
        private static readonly ObjectId DoubleXpId = ObjectId.Parse("5db772de03eb7230a1b5bba1");
        private static readonly ObjectId FastXpId = ObjectId.Parse("5dbc2dd103eb7230a1b5bba5");

        public Xp(IHttpClientFactory http, IMongoService mongo)
        {
            _http = http;
            _mongo = mongo;
        }
        
        [RokiCommand, Description, Usage, Aliases]
        public async Task Experience(IUser user = null)
        {
            if (user == null || user.IsBot)
            {
                user = Context.User;
            }

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            
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

            User dbUser = await _mongo.Context.GetUserAsync(user.Id);
            var xp = new XpLevel(dbUser.Xp);
            int rank = await _mongo.Context.GetUserXpRankAsync(dbUser).ConfigureAwait(false);
            bool doubleXp = dbUser.Subscriptions.Any(x => x.Id == DoubleXpId);
            bool fastXp = dbUser.Subscriptions.Any(x => x.Id == FastXpId);

            await using Stream xpImage = XpDrawExtensions.GenerateXpBar(avatar, 
                xp.ProgressXp, xp.RequiredXp, $"{xp.TotalXp}", $"{xp.Level}", $"{rank}", 
                user.Username, user.Discriminator, dbUser.LastLevelUp, doubleXp, fastXp);
            await Context.Channel.SendFileAsync(xpImage, $"xp-{user.Id}.png").ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task XpLeaderboard(int page = 0)
        {
            if (page < 0)
                return;
            if (page > 0)
                page -= 1;
            IEnumerable<User> list = await _mongo.Context.GetXpLeaderboardAsync(page).ConfigureAwait(false);
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle("XP Leaderboard");
            int i = 9 * page + 1;
            foreach (User user in list)
            {
                embed.AddField($"#{i++} {user.Username}#{user.Discriminator}", $"Level `{new XpLevel(user.Xp).Level:N0}` - `{user.Xp:N0}` xp");
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

            if (!notify.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                !notify.Equals("dm", StringComparison.OrdinalIgnoreCase) ||
                !notify.Equals("server", StringComparison.OrdinalIgnoreCase))
            {
                await Context.Channel.SendErrorAsync("Not a valid option. Options are none, dm, or server.")
                    .ConfigureAwait(false);
                return;
            }

            if (!isAdmin)
            {
                await _mongo.Context.UpdateUserNotificationPreferenceAsync(Context.User.Id, notify).ConfigureAwait(false);
            }
            else
            {
                await _mongo.Context.UpdateUserNotificationPreferenceAsync(user.Id, notify).ConfigureAwait(false);
            }
            
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
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
            
            List<XpReward> rewards = (await _mongo.Context.GetGuildAsync(Context.Guild.Id).ConfigureAwait(false)).XpRewards;
            if (rewards == null || rewards.Count == 0)
            {
                await Context.Channel.SendErrorAsync("There are currently no XP rewards setup for this server.").ConfigureAwait(false);
                return;
            }

            List<XpReward> sorted = rewards.OrderByDescending(r => r.Level).ToList();
            
            int totalPages = sorted.Count / 9;
            if (page > totalPages)
            {
                page = totalPages;
            }
            
            EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle("XP Rewards")
                .WithFooter($"Page {page + 1}/{totalPages + 1}");

            if (!id)
            {
                embed.WithDescription(string.Join("\n", sorted
                    .Skip(page * 9)
                    .Take(9)
                    .Select(r => r.Type == "currency"
                        ? $"Level `{r.Level}` - `{int.Parse(r.Reward):N0}` {Roki.Properties.CurrencyIcon}"
                        : $"Level `{r.Level}` - <@&{r.Reward}>")));
            }
            else
            {
                embed.WithDescription(string.Join("\n", sorted
                    .Skip(page * 9)
                    .Take(9)
                    .Select(r => r.Type == "currency"
                        ? $"`{r.Id}` Level `{r.Level}` - `{int.Parse(r.Reward):N0}` {Roki.Properties.CurrencyIcon}"
                        : $"`{r.Id}` Level `{r.Level}` - <@&{r.Reward}>")));
            }

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task XpRewardAdd(RewardType rewardType, int level, string reward)
        {
            if (rewardType == RewardType.Currency)
            {
                if (!int.TryParse(reward, out int rewardAmount))
                {
                    await Context.Channel.SendErrorAsync("Make sure you enter a valid number for the currency reward.").ConfigureAwait(false);
                    return;
                }

                var curReward = new XpReward
                {
                    Id = ObjectId.GenerateNewId(),
                    Level = level,
                    Reward = rewardAmount.ToString(),
                    Type = "currency"
                };
                
                await _mongo.Context.AddXpRewardAsync(Context.Guild.Id, curReward).ConfigureAwait(false);
                
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle("XP Reward Added")
                        .WithDescription("Successfully added a new XP reward.\n" +
                                         $"Reward ID: `{curReward.Id.GetHexId()}`\n" +
                                         $"XP Level: `{level}`\n" +
                                         "Reward Type: currency\n" +
                                         $"Reward Amount: `{rewardAmount:N0}`"))
                    .ConfigureAwait(false);
                
                return;
            }

            ulong roleId = Context.Message.MentionedRoleIds.FirstOrDefault();
            if (roleId == 0)
            {
                if (!ulong.TryParse(reward, out roleId))
                {
                    await Context.Channel.SendErrorAsync("Please specify a role for the XP role reward.\nIf the role is not mentionable, use the role ID instead")
                        .ConfigureAwait(false);
                    return;
                }
            }

            IRole role = Context.Guild.GetRole(roleId);
            if (role == null)
            {
                await Context.Channel.SendErrorAsync("Could not find that role. Please check the role ID again.").ConfigureAwait(false);
                return;
            }

            var roleReward = new XpReward
            {
                Id = ObjectId.GenerateNewId(),
                Level = level,
                Reward = role.Id.ToString(),
                Type = "role"
            };
            
            await _mongo.Context.AddXpRewardAsync(Context.Guild.Id, roleReward).ConfigureAwait(false);
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithTitle("XP Reward Added")
                    .WithDescription("Successfully added a new XP reward.\n" +
                                     $"Reward ID: `{roleReward.Id.GetHexId()}`\n" +
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
                await Context.Channel.SendErrorAsync($"Please specify the XP reward ID. You can obtain the IDs by using `{Roki.Properties.Prefix}xpr <page_num>`.")
                    .ConfigureAwait(false);
                return;
            }

            UpdateResult success = await _mongo.Context.RemoveXpRewardAsync(Context.Guild.Id, id).ConfigureAwait(false);
            if (success.IsAcknowledged && success.ModifiedCount > 1)
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle("XP Reward Removed.")
                        .WithDescription($"Successfully removed reward with ID: `{id}`"))
                    .ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.SendErrorAsync($"Something went wrong when trying to remove the reward with ID: `{id}`\nPlease double check the ID and try again.")
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