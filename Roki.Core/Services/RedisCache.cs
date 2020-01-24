using NLog;
using StackExchange.Redis;

namespace Roki.Core.Services
{
    public interface IRedisCache
    {
        public ConnectionMultiplexer Redis { get; }
    }

    public class RedisCache : IRedisCache
    {
        private readonly Logger _log;
        public ConnectionMultiplexer Redis { get; }
        
        public RedisCache(string credentials)
        {
            _log = LogManager.GetCurrentClassLogger();

            var config = ConfigurationOptions.Parse(credentials);
            
            Redis = ConnectionMultiplexer.Connect(config);
        }
    }
}