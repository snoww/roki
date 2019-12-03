using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Modules.Games.Common;

namespace Roki.Modules.Games.Services
{
    public class JeopardyService : IRService
    {
        private DbService _db;

        public JeopardyService(DbService db)
        {
            _db = db;
        }

        public List<JeopardyModel> GenerateGame()
        {
            var qs = new List<JeopardyModel>();
            for (int i = 0; i < 2; i++)
            {
                var cat = GetRandomCategories();
                var valid = ValidateGame(GetRandomQuestions(cat));
                while (valid == null)
                {
                    cat = GetRandomCategories();
                    valid = ValidateGame(GetRandomQuestions(cat));
                }
                qs.AddRange(valid);
            }
            return qs;
        }

        private List<JeopardyModel> ValidateGame(List<JeopardyModel> game)
        {
            var validated = new List<JeopardyModel>();
            try
            {
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

        private List<JeopardyModel> GetRandomQuestions(int categoryId)
        {
            using var uow = _db.GetDbContext();
            var query = from clue in uow.Context.Set<Clues>()
//                join airdate in uow.Context.Set<AirDate>() on clue.Game equals airdate.Game
                join document in uow.Context.Set<Documents>() on clue.Id equals document.Id
                join classification in uow.Context.Set<Classification>() on clue.Id equals classification.ClueId
                join categories in uow.Context.Set<Categories>() on classification.CategoryId equals categories.Id
                where categories.Id == categoryId
                select new JeopardyModel
                {
                    Category = categories.Category,
                    Clue = document.Clue,
                    Answer = document.Answer,
                    Value = clue.Value
                };

            return query.ToList();
        }

        private int GetRandomCategories()
        {
            using var uow = _db.GetDbContext();
            var rng = new Random();
            var count = uow.Context.Categories.Count();
            return uow.Context.Categories.Skip(rng.Next(0, count)).Take(1).First().Id;
        }
    }
}