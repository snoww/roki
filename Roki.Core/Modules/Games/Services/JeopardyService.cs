using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Roki.Extensions;
using Roki.Modules.Games.Common;
using Roki.Services;
using Roki.Services.Database.Data;

namespace Roki.Modules.Games.Services
{
    public class JeopardyService : IRokiService
    {
        private readonly DbService _db;
        public readonly ConcurrentDictionary<ulong, Jeopardy> ActiveGames = new ConcurrentDictionary<ulong, Jeopardy>();

        public JeopardyService(DbService db)
        {
            _db = db;
        }

        public Dictionary<string, List<JClue>> GenerateGame(int categories)
        {
            var qs = new Dictionary<string, List<JClue>>();
            for (int i = 0; i < categories; i++)
            {
                var cat = GetRandomCategory();
                var valid = ValidateGame(GetRandomQuestions(cat.Id));
                while (valid == null)
                {
                    cat = GetRandomCategory();
                    valid = ValidateGame(GetRandomQuestions(cat.Id));
                }
                qs.Add(cat.Category, valid);
            }
            return qs;
        }

        public JClue GetFinalJeopardy()
        {
            using var uow = _db.GetDbContext();
            var query = from clue in uow.Context.Set<Clues>()
                join document in uow.Context.Set<Documents>() on clue.Id equals document.Id
                join classification in uow.Context.Set<Classification>() on clue.Id equals classification.ClueId
                join categories in uow.Context.Set<Categories>() on classification.CategoryId equals categories.Id
                where clue.Round == 3
                select new JClue
                {
                    Category = categories.Category,
                    Clue = document.Clue,
                    Answer = document.Answer,
                    Value = clue.Value
                };

            JClue final;
            do
            {
                var skip = new Random().Next(0, query.Count());
                final = query.Skip(skip).First();
                if (string.IsNullOrWhiteSpace(final.Clue.SanitizeStringFull()) || string.IsNullOrWhiteSpace(final.Answer.SanitizeStringFull()))
                {
                    final = null;
                }
            } while (final == null);

            final.SanitizeAnswer();
            return final;
        }

        private List<JClue> ValidateGame(IList<JClue> game)
        {
            var validated = new List<JClue>();
            try
            {
                game.Shuffle();
                var values = new List<int>{200, 400, 600, 800, 1000};

                foreach (var clue in game)
                {
                    if (values.Count == 0) break;
                    var value = values.FirstOrDefault(c => c == clue.Value || c == clue.Value - 1000);
                    if (value == 0) continue;
                    if (string.IsNullOrWhiteSpace(clue.Clue.SanitizeStringFull()) ||
                        string.IsNullOrWhiteSpace(clue.Answer.SanitizeStringFull())) continue;
                    clue.Value = value;
                    clue.SanitizeAnswer();
                    validated.Add(clue);
                    values.Remove(value);
                }

                return values.Count == 0 ? validated.OrderBy(c => c.Value).ToList() : null;
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

        private Categories GetRandomCategory()
        {
            using var uow = _db.GetDbContext();
            var rng = new Random();
            var count = uow.Context.Categories.Count();
            return uow.Context.Categories.Skip(rng.Next(0, count)).Take(1).First();
        }
    }
}