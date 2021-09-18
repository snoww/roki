using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDatabase _cache;
        
        public ConfigurationService(IServiceScopeFactory scopeFactory, IRedisCache cache)
        {
            _scopeFactory = scopeFactory;
            _cache = cache.Redis.GetDatabase();
        }

        public async Task<GuildConfig> GetGuildConfigAsync(ulong guildId)
        {
            // RedisValue cacheGuildConfig = await _cache.StringGetAsync($"config:{guildId}");
            // if (cacheGuildConfig.HasValue)
            // {
            //     return JsonSerializer.Deserialize<GuildConfig>(cacheGuildConfig.ToString());
            // }

            using IServiceScope scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RokiContext>();

            GuildConfig guildConfig = await context.GuildConfigs.AsNoTracking().SingleOrDefaultAsync(x => x.GuildId == guildId);
            // await _cache.StringSetAsync($"config:{guildId}", JsonSerializer.Serialize(guildConfig), TimeSpan.FromDays(1));
            return guildConfig;
        } 
        
        public async Task<ChannelConfig> GetChannelConfigAsync(ulong channelId)
        {
            RedisValue cacheChannelConfig = await _cache.StringGetAsync($"config:{channelId}");
            if (cacheChannelConfig.HasValue)
            {
                return JsonSerializer.Deserialize<ChannelConfig>(cacheChannelConfig.ToString());
            }
            
            using IServiceScope scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RokiContext>();

            ChannelConfig channelConfig = await context.ChannelConfigs.AsNoTracking().SingleOrDefaultAsync(x => x.ChannelId == channelId);
            await _cache.StringSetAsync($"config:{channelId}", JsonSerializer.Serialize(channelConfig), TimeSpan.FromDays(1));
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
            await _cache.StringSetAsync($"config:{guildId}:prefix", guildConfig.Prefix, TimeSpan.FromDays(1));
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
            await _cache.StringSetAsync($"config:{channelId}:logging", channelConfig.Logging, TimeSpan.FromDays(1));
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
            await _cache.StringSetAsync($"config:{channelId}:currency", channelConfig.CurrencyGen, TimeSpan.FromDays(1));
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
            await _cache.StringSetAsync($"config:{channelId}:xp", channelConfig.XpGain, TimeSpan.FromDays(1));
            return channelConfig.XpGain;
        }
    }
}