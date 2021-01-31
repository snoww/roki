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

        Task<string> GetGuildPrefix(ulong guildId);
        Task<bool> LoggingEnabled(ITextChannel channel);
        Task<bool> CurrencyGenEnabled(ITextChannel channel);
        Task<bool> XpGainEnabled(ITextChannel channel);

        Task UpdateGuildConfigAsync(ulong guildId, GuildConfig config);
        Task UpdateChannelConfigAsync(ITextChannel channel, ChannelConfig config);
        Task UpdatePrefix(ulong guildId, string prefix);
        Task UpdateLogging(ulong channelId, bool enable);
        Task UpdateCurGen(ulong channelId, bool enable);
        Task UpdateXpGain(ulong channelId, bool enable);
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

        public async Task<string> GetGuildPrefix(ulong guildId)
        {
            RedisValue cachePrefix = await _cache.StringGetAsync($"config:guild:{guildId}:prefix");
            if (cachePrefix.HasValue)
            {
                return cachePrefix.ToString();
            }

            GuildConfig guildConfig = await GetGuildConfigAsync(guildId);
            await _cache.StringSetAsync($"config:guild:{guildId}:prefix", guildConfig.Prefix, TimeSpan.FromDays(7));
            return guildConfig.Prefix;
        }

        public async Task<bool> LoggingEnabled(ITextChannel channel)
        {
            RedisValue cacheLogging = await _cache.StringGetAsync($"config:channel:{channel.Id}:logging");
            if (cacheLogging.HasValue)
            {
                return (bool) cacheLogging;
            }

            ChannelConfig channelConfig = await GetChannelConfigAsync(channel);
            await _cache.StringSetAsync($"config:channel:{channel.Id}:logging", channelConfig.Logging, TimeSpan.FromDays(7));
            return channelConfig.Logging;
        }

        public async Task<bool> CurrencyGenEnabled(ITextChannel channel)
        {
            RedisValue cacheCurr = await _cache.StringGetAsync($"config:channel:{channel.Id}:currency");
            if (cacheCurr.HasValue)
            {
                return (bool) cacheCurr;
            }

            ChannelConfig channelConfig = await GetChannelConfigAsync(channel);
            await _cache.StringSetAsync($"config:channel:{channel.Id}:currency", channelConfig.CurrencyGeneration, TimeSpan.FromDays(7));
            return channelConfig.CurrencyGeneration;
        }

        public async Task<bool> XpGainEnabled(ITextChannel channel)
        {
            RedisValue cacheXp = await _cache.StringGetAsync($"config:channel:{channel.Id}:xp");
            if (cacheXp.HasValue)
            {
                return (bool) cacheXp;
            }

            ChannelConfig channelConfig = await GetChannelConfigAsync(channel);
            await _cache.StringSetAsync($"config:channel:{channel.Id}:xp", channelConfig.XpGain, TimeSpan.FromDays(7));
            return channelConfig.XpGain;
        }

        // todo
        public async Task UpdateGuildConfigAsync(ulong guildId, GuildConfig guildConfig)
        {
            await _cache.StringSetAsync($"config:guild:{guildId}", JsonSerializer.Serialize(guildConfig), TimeSpan.FromDays(7));
            await _context.UpdateGuildConfigAsync(guildId, guildConfig);
        }

        public async Task UpdateChannelConfigAsync(ITextChannel channel, ChannelConfig channelConfig)
        {
            await _cache.StringSetAsync($"config:channel:{channel.Id}", JsonSerializer.Serialize(channelConfig), TimeSpan.FromDays(7));
            await _context.UpdateChannelConfigAsync(channel, channelConfig);
        }

        public async Task UpdatePrefix(ulong guildId, string prefix)
        {
            await _cache.StringSetAsync($"config:guild:{guildId}:prefix", prefix, TimeSpan.FromDays(7));
        }

        public async Task UpdateLogging(ulong channelId, bool enable)
        {
            await _cache.StringSetAsync($"config:channel:{channelId}:logging", enable, TimeSpan.FromDays(7));
        }

        public async Task UpdateCurGen(ulong channelId, bool enable)
        {
            await _cache.StringSetAsync($"config:channel:{channelId}:currency", enable, TimeSpan.FromDays(7));
        }

        public async Task UpdateXpGain(ulong channelId, bool enable)
        {
            await _cache.StringSetAsync($"config:channel:{channelId}:xp", enable, TimeSpan.FromDays(7));
        }
    }
}