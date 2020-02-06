using NLog;
using StackExchange.Redis;

namespace Roki.Services
{
    public sealed class RedisCache
    {
        public static RedisCache Instance { get; } = new RedisCache();
        public IDatabase Cache { get; }

        private RedisCache()
        {
            Cache = ConnectionMultiplexer.Connect("localhost").GetDatabase();
        }
    }
}