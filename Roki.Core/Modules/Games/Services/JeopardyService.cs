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

        public List<JeopardyModel> GenerateQuestions()
        {
            var cats = GetRandomCategories();
            var qs = GetRandomQuestions(cats.First());
            qs.AddRange(GetRandomQuestions(cats.Last()));
            return qs;
        }

        private List<JeopardyModel> GetRandomQuestions(int categoryId)
        {
            using var uow = _db.GetDbContext();
            var query = from clue in uow.Context.Set<Clues>()
                join airdate in uow.Context.Set<AirDate>() on clue.Game equals airdate.Game
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

        private List<int> GetRandomCategories()
        {
            using var uow = _db.GetDbContext();
            var rng = new Random();
            var count = uow.Context.Categories.Count();
            return new List<int>
            {
                uow.Context.Categories.Skip(rng.Next(0, count)).Take(1).First().Id,
                uow.Context.Categories.Skip(rng.Next(0, count)).Take(1).First().Id,
            };
        }
    }
}