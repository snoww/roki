using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
using Roki.Modules.Games.Common;

namespace Roki.Modules.Games.Services
{
    public class JeopardyService : IRService
    {
        private readonly DbService _db;
        public ConcurrentDictionary<ulong, Jeopardy> ActiveGames = new ConcurrentDictionary<ulong, Jeopardy>();

        public JeopardyService(DbService db)
        {
            _db = db;
        }

        public Dictionary<string, List<JClue>> GenerateGame()
        {
            var qs = new Dictionary<string, List<JClue>>();
            for (int i = 0; i < 2; i++)
            {
                var cat = GetRandomCategories();
                var valid = ValidateGame(GetRandomQuestions(cat.Id));
                while (valid == null)
                {
                    cat = GetRandomCategories();
                    valid = ValidateGame(GetRandomQuestions(cat.Id));
                }
                qs.Add(cat.Category, valid);
            }
            return qs;
        }

        private List<JClue> ValidateGame(IList<JClue> game)
        {
            var validated = new List<JClue>();
            try
            {
                game.Shuffle();
                validated.Add(game.First(g => g.Value == 200));
                validated.Add(game.First(g => g.Value == 400));
                validated.Add(game.First(g => g.Value == 600));
                validated.Add(game.First(g => g.Value == 800));
                validated.Add(game.First(g => g.Value == 1000));
                return validated;
            }
            catch
            {
                return null;
            }  
        }

        private List<JClue> GetRandomQuestions(int categoryId, int round = 1)
        {
            using var uow = _db.GetDbContext();
            var query = from clue in uow.Context.Set<Clues>()
//                join airdate in uow.Context.Set<AirDate>() on clue.Game equals airdate.Game
                join document in uow.Context.Set<Documents>() on clue.Id equals document.Id
                join classification in uow.Context.Set<Classification>() on clue.Id equals classification.ClueId
                join categories in uow.Context.Set<Categories>() on classification.CategoryId equals categories.Id
                where categories.Id == categoryId && clue.Round == round
                select new JClue
                {
                    Category = categories.Category,
                    Clue = document.Clue,
                    Answer = document.Answer,
                    Value = clue.Value
                };

            return query.ToList();
        }

        private Categories GetRandomCategories()
        {
            using var uow = _db.GetDbContext();
            var rng = new Random();
            var count = uow.Context.Categories.Count();
            return uow.Context.Categories.Skip(rng.Next(0, count)).Take(1).First();
        }
    }
}