using System;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Roki.Services.Database;
using Roki.Services.Database.Models;
using StackExchange.Redis;

namespace Roki.Services
{
    public interface IConfigurationService
    {
        Task<GuildConfig> GetGuildConfigAsync(ulong guildId);
        Task<ChannelConfig> GetChannelConfigAsync(ulong channelId);

        Task<string> GetGuildPrefix(ulong guildId);
        Task<bool> LoggingEnabled(ulong channelId);
        Task<bool> CurrencyGenEnabled(ulong channelId);
        Task<bool> XpGainEnabled(ulong channelId);
    }
    
    public class ConfigurationService : IConfigurationService
    {
        private readonly IRokiDbService _service;
        private readonly IDatabase _cache;
        
        public ConfigurationService(IRokiDbService service, IRedisCache cache)
        {
            _service = service;
            _cache = cache.Redis.GetDatabase();
        }

        public async Task<GuildConfig> GetGuildConfigAsync(ulong guildId)
        {
            RedisValue cacheGuildConfig = await _cache.StringGetAsync($"config:{guildId}");
            if (cacheGuildConfig.HasValue)
            {
                return JsonSerializer.Deserialize<GuildConfig>(cacheGuildConfig.ToString());
            }

            GuildConfig guildConfig = await _service.GetGuildConfigAsync(guildId);
            await _cache.StringSetAsync($"config:{guildId}", JsonSerializer.Serialize(guildConfig), TimeSpan.FromDays(7));
            return guildConfig;
        } 
        
        public async Task<ChannelConfig> GetChannelConfigAsync(ulong channelId)
        {
            RedisValue cacheChannelConfig = await _cache.StringGetAsync($"config:{channelId}");
            if (cacheChannelConfig.HasValue)
            {
                return JsonSerializer.Deserialize<ChannelConfig>(cacheChannelConfig.ToString());
            }

            ChannelConfig channelConfig = await _service.GetChannelConfigAsync(channelId);
            await _cache.StringSetAsync($"config:{channelId}", JsonSerializer.Serialize(channelConfig), TimeSpan.FromDays(7));
            return channelConfig;
        }

        public async Task<string> GetGuildPrefix(ulong guildId)
        {
            RedisValue cachePrefix = await _cache.StringGetAsync($"config:{guildId}:prefix");
            if (cachePrefix.HasValue)
            {
                return cachePrefix.ToString();
            }

            GuildConfig guildConfig = await GetGuildConfigAsync(guildId);
            await _cache.StringSetAsync($"config:{guildId}:prefix", guildConfig.Prefix, TimeSpan.FromDays(7));
            return guildConfig.Prefix;
        }

        public async Task<bool> LoggingEnabled(ulong channelId)
        {
            RedisValue cacheLogging = await _cache.StringGetAsync($"config:{channelId}:logging");
            if (cacheLogging.HasValue)
            {
                return (bool) cacheLogging;
            }

            ChannelConfig channelConfig = await GetChannelConfigAsync(channelId);
            await _cache.StringSetAsync($"config:{channelId}:logging", channelConfig.Logging, TimeSpan.FromDays(7));
            return channelConfig.Logging;
        }

        public async Task<bool> CurrencyGenEnabled(ulong channelId)
        {
            RedisValue cacheCurr = await _cache.StringGetAsync($"config:{channelId}:currency");
            if (cacheCurr.HasValue)
            {
                return (bool) cacheCurr;
            }

            ChannelConfig channelConfig = await GetChannelConfigAsync(channelId);
            await _cache.StringSetAsync($"config:{channelId}:currency", channelConfig.CurrencyGen, TimeSpan.FromDays(7));
            return channelConfig.CurrencyGen;
        }

        public async Task<bool> XpGainEnabled(ulong channelId)
        {
            RedisValue cacheXp = await _cache.StringGetAsync($"config:{channelId}:xp");
            if (cacheXp.HasValue)
            {
                return (bool) cacheXp;
            }

            ChannelConfig channelConfig = await GetChannelConfigAsync(channelId);
            await _cache.StringSetAsync($"config:{channelId}:xp", channelConfig.Xp, TimeSpan.FromDays(7));
            return channelConfig.Xp;
        }
    }
}