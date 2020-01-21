using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IGuildRepository : IRepository<Guild>
    {
        Task<Guild> GetOrCreateGuildAsync(SocketGuild guild);
        Task<XpReward> AddXpReward(int level, string type, string reward);
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

        public Task<XpReward> AddXpReward(int level, string type, string reward)
        {
            throw new System.NotImplementedException();
        }
    }
}