using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Roki.Services.Database.Core;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IGuildRepository : IRepository<Guild>
    {
        Task<Guild> GetOrCreateGuildAsync(SocketGuild guild);
        Task<List<XpReward>> GetXpRewardsAsync(ulong guildId, int level);
        Task<List<XpReward>> GetAllXpRewardsAsync(ulong guildId);
        Task<XpReward> AddXpRewardAsync(ulong guildId, int level, string type, string reward);
        Task<bool> RemoveXpRewardAsync(ulong guildId, string rewardId);
    }
    
    public class GuildRepository : Repository<Guild>, IGuildRepository
    {
        public GuildRepository(DbContext context) : base(context)
        {
        }

        public async Task<Guild> GetOrCreateGuildAsync(SocketGuild guild)
        {
            var gd = await Set.FirstOrDefaultAsync(g => g.GuildId == guild.Id).ConfigureAwait(false);
            if (gd != null) return gd;
            var newGuild = Set.Add(new Guild
            {
                GuildId = guild.Id,
                Name = guild.Name,
                IconId = guild.IconId,
                ChannelCount = guild.Channels.Count,
                MemberCount = guild.MemberCount,
                EmoteCount = guild.Emotes.Count,
                OwnerId = guild.OwnerId,
                RegionId = guild.VoiceRegionId,
                CreatedAt = guild.CreatedAt
            });
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return newGuild.Entity;
        }

        public async Task<List<XpReward>> GetXpRewardsAsync(ulong guildId, int level)
        {
            var rewards = await GetAllXpRewardsAsync(guildId).ConfigureAwait(false);
            return rewards?.Where(r => r.XpLevel == level).ToList();
        }

        public async Task<List<XpReward>> GetAllXpRewardsAsync(ulong guildId)
        {
            var guild = await Set.FirstAsync(g => g.GuildId == guildId).ConfigureAwait(false);
            var rewardsRaw = guild.XpRewards;
            return string.IsNullOrWhiteSpace(rewardsRaw) 
                ? null 
                : JsonSerializer.Deserialize<List<XpReward>>(rewardsRaw);
        }

        public async Task<XpReward> AddXpRewardAsync(ulong guildId, int level, string type, string reward)
        {
            var guild = await Set.FirstAsync(g => g.GuildId == guildId).ConfigureAwait(false);
            var rewardsRaw = guild.XpRewards;

            List<XpReward> rewards;
            int elements;
            
            try
            {
                rewards = JsonSerializer.Deserialize<List<XpReward>>(rewardsRaw);
                elements = rewards.Count;
            }
            catch (Exception)
            {
                rewards = new List<XpReward>();
                elements = 0;
            }

            var uid = Guid.NewGuid().ToString().Substring(0, 7);
            var xpReward = new XpReward
            {
                Id = uid,
                XpLevel = level,
                Type = type,
                Reward = reward
            };
            
            if (elements == 0 || string.IsNullOrWhiteSpace(rewardsRaw))
            {
                var rewardJson = JsonSerializer.Serialize(xpReward);
                guild.XpRewards = $"[{rewardJson}]";
                await Context.SaveChangesAsync().ConfigureAwait(false);
            }
            else
            {
                rewards.Add(xpReward);
                guild.XpRewards = JsonSerializer.Serialize(rewards);
            }
            
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return xpReward;
        }

        public async Task<bool> RemoveXpRewardAsync(ulong guildId, string rewardId)
        {
            var guild = await Set.FirstAsync(g => g.GuildId == guildId).ConfigureAwait(false);
            var rewardsRaw = guild.XpRewards;

            List<XpReward> rewards;
            int elements;
            
            try
            {
                rewards = JsonSerializer.Deserialize<List<XpReward>>(rewardsRaw);
                elements = rewards.Count;
            }
            catch (Exception)
            {
                rewards = new List<XpReward>();
                elements = 0;
            }

            if (elements == 0 || string.IsNullOrWhiteSpace(rewardsRaw))
            {
                return false;
            }

            var reward = rewards.FirstOrDefault(r => r.Id.Equals(rewardId, StringComparison.OrdinalIgnoreCase));
            if (reward == null)
            {
                return false;
            }

            rewards.Remove(reward);
            guild.XpRewards = JsonSerializer.Serialize(rewards);
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }
    }
}