using NLog;
using StackExchange.Redis;

namespace Roki.Services
{
    public interface IRedisCache
    {
        public ConnectionMultiplexer Redis { get; }
    }

    public class RedisCache : IRedisCache
    {
        public ConnectionMultiplexer Redis { get; }
        
        public RedisCache(string credentials)
        {
            var config = ConfigurationOptions.Parse(credentials);
            
            Redis = ConnectionMultiplexer.Connect(config);
        }
    }
}