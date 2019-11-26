using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IChannelRepository : IRepository<Channel>
    {
        Task<Channel> GetOrCreateChannelAsync(SocketTextChannel channel);
    }
    
    public class ChannelRepository : Repository<Channel>, IChannelRepository
    {
        public ChannelRepository(DbContext context) : base(context)
        {
        }

        public async Task<Channel> GetOrCreateChannelAsync(SocketTextChannel channel)
        {
            var chn = await Set.FirstOrDefaultAsync(c => c.ChannelId == channel.Id).ConfigureAwait(false);
            if (chn != null) return chn;
            var newChannel = Set.Add(new Channel
            {
                ChannelId = channel.Id,
                Name = channel.Name,
                GuildId = channel.Guild.Id,
                GuildName = channel.Guild.Name,
                UserCount = channel.Users.Count,
                IsNsfw = channel.IsNsfw
            });
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return newChannel.Entity;
        }
    }
}