using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Roki.Core.Services;
using Roki.Modules.Games.Common;
using StackExchange.Redis;

namespace Roki.Modules.Games.Services
{
    public class ShowdownService : IRokiService
    {
        private readonly IDatabase _cache;
        
        public readonly ConcurrentDictionary<ulong, Showdown> ActiveGames = new ConcurrentDictionary<ulong, Showdown>();

        public ShowdownService(IRedisCache cache)
        {
            _cache = cache.Redis.GetDatabase();
        }

        public async Task<string> GetBetPokemonGame(string uid)
        {
            var game = await _cache.StringGetAsync($"pokemon:{uid}").ConfigureAwait(false);
            if (game.HasValue)
            {
                return game.ToString();
            }
            
            var logs = await File.ReadAllLinesAsync(@"./data/pokemon-logs/battle-logs").ConfigureAwait(false);
            return (from log in logs where log.StartsWith(uid, StringComparison.OrdinalIgnoreCase) select log.Substring(9)).FirstOrDefault();
        }
    }
}