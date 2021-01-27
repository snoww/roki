using System;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Roki.Services.Database;
using Roki.Services.Database.Maps;
using StackExchange.Redis;

namespace Roki.Services
{
    public interface IConfigurationService
    {
        Task<GuildConfig> GetGuildConfigAsync(ulong guildId);
        Task<ChannelConfig> GetChannelConfigAsync(ITextChannel channel);

        Task SetGuildConfigAsync(GuildConfig config);
        Task SetChannelConfigAsync(ChannelConfig config);
    }
    
    public class ConfigurationService : IConfigurationService
    {
        private readonly IMongoContext _context;
        private readonly IDatabase _cache;
        
        public ConfigurationService(IMongoContext context, IRedisCache cache)
        {
            _context = context;
            _cache = cache.Redis.GetDatabase();
        }

        public async Task<GuildConfig> GetGuildConfigAsync(ulong guildId)
        {
            RedisValue cacheGuildConfig = await _cache.StringGetAsync($"config:guild:{guildId}");
            if (cacheGuildConfig.HasValue)
            {
                return JsonSerializer.Deserialize<GuildConfig>(cacheGuildConfig.ToString());
            }

            GuildConfig guildConfig = await _context.GetGuildConfigAsync(guildId);
            await _cache.StringSetAsync($"config:guild:{guildId}", JsonSerializer.Serialize(guildConfig), TimeSpan.FromDays(7));
            return guildConfig;
        } 
        
        public async Task<ChannelConfig> GetChannelConfigAsync(ITextChannel channel)
        {
            RedisValue cacheChannelConfig = await _cache.StringGetAsync($"config:channel:{channel.Id}");
            if (cacheChannelConfig.HasValue)
            {
                return JsonSerializer.Deserialize<ChannelConfig>(cacheChannelConfig.ToString());
            }

            ChannelConfig channelConfig = await _context.GetChannelConfigAsync(channel);
            await _cache.StringSetAsync($"config:channel:{channel.Id}", JsonSerializer.Serialize(channelConfig), TimeSpan.FromDays(7));
            return channelConfig;
        }

        // todo
        public Task SetGuildConfigAsync(GuildConfig config)
        {
            throw new NotImplementedException();
        }

        public Task SetChannelConfigAsync(ChannelConfig config)
        {
            throw new NotImplementedException();
        }
    }
}