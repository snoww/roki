using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Roki.Modules.Games.Common;
using Roki.Services;
using StackExchange.Redis;

namespace Roki.Modules.Games.Services
{
    public class ShowdownService : IRokiService
    {
        private static readonly IDatabase Cache = RedisCache.Instance.Cache;
        
        public readonly ConcurrentDictionary<ulong, Showdown> ActiveGames = new ConcurrentDictionary<ulong, Showdown>();

        public ShowdownService()
        {
        }

        public async Task<string> GetBetPokemonGame(string uid)
        {
            var game = await Cache.StringGetAsync($"pokemon:{uid}").ConfigureAwait(false);
            if (game.HasValue)
            {
                return game.ToString();
            }
            
            var logs = await File.ReadAllLinesAsync(@"./data/pokemon-logs/battle-logs").ConfigureAwait(false);
            return (from log in logs where log.StartsWith(uid, StringComparison.OrdinalIgnoreCase) select log.Substring(9)).FirstOrDefault();
        }
    }
}