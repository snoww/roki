using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Roki.Modules.Games.Common;
using Roki.Services;

namespace Roki.Modules.Games.Services
{
    public class JeopardyService : IRokiService
    {
        private readonly IMongoService _mongo;

        public readonly ConcurrentDictionary<ulong, Jeopardy> ActiveGames = new();

        public JeopardyService(IMongoService mongo)
        {
            _mongo = mongo;
        }

        public async Task<Dictionary<string, List<JClue>>> GenerateGame(int number)
        {
            Dictionary<string, List<JClue>> game = await _mongo.Context.GetRandomJeopardyCategoriesAsync(number).ConfigureAwait(false);
            return game;
        }

        public async Task<JClue> GenerateFinalJeopardy()
        {
            return await _mongo.Context.GetFinalJeopardyAsync().ConfigureAwait(false);
        }
    }
}