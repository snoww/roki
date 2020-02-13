using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Roki.Modules.Games.Common;
using Roki.Services;

namespace Roki.Modules.Games.Services
{
    public class JeopardyService : IRokiService
    {
        private readonly IMongoService _mongo;
        
        public readonly ConcurrentDictionary<ulong, Jeopardy> ActiveGames = new ConcurrentDictionary<ulong, Jeopardy>();

        public JeopardyService(IMongoService mongo)
        {
            _mongo = mongo;
        }

        public async Task<Dictionary<string, List<JClue>>> GenerateGame(int number)
        {
            var game = await _mongo.Context.GetRandomJeopardyCategoriesAsync(number).ConfigureAwait(false);
            foreach (var clue in game.Cast<List<JClue>>().SelectMany(clues => clues))
            {
                clue.SanitizeAnswer();
            }

            return game;
        }

        public async Task<JClue> GenerateFinalJeopardy()
        {
            var clue =  await _mongo.Context.GetFinalJeopardyAsync().ConfigureAwait(false);
            clue.SanitizeAnswer();

            return clue;
        }
    }
}