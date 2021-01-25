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
            Redis = ConnectionMultiplexer.Connect(ConfigurationOptions.Parse(credentials));
        }

        public RedisCache()
        {
            Redis = ConnectionMultiplexer.Connect("localhost");
        }
    }
}