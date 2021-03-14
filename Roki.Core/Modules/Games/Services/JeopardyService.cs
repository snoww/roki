using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Modules.Games.Common;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;

namespace Roki.Modules.Games.Services
{
    public class JeopardyService : IRokiService
    {
        private readonly RokiContext _context;

        public readonly ConcurrentDictionary<ulong, Jeopardy> ActiveGames = new();

        public JeopardyService(RokiContext context)
        {
            _context = context;
        }

        public async Task<List<Category>> GenerateGame(int number)
        {
            List<Category> categories = await _context.Categories
                .Include(x => x.Clues
                    .OrderBy(y => y.Value))
                .AsNoTracking()
                .Where(x => x.Round == 1 || x.Round == 2)
                .OrderBy(x => Guid.NewGuid())
                .Take(number)
                .ToListAsync();

            foreach (Category category in categories)
            {
                Parallel.ForEach(category.Clues, clue =>
                {
                    clue.PrepareAnswer();
                    if (category.Round == 2)
                    {
                        // no double jeopardy's yet
                        // adjust value to regular 
                        clue.AdjustValue();
                    }
                });
            }

            return categories;
        }

        public async Task<Category> GenerateFinalJeopardy()
        {
            Category final = await _context.Categories.Include(x => x.Clues).AsNoTracking()
                .Where(x => x.Round == 3)
                .OrderBy(x => Guid.NewGuid())
                .Take(1)
                .SingleAsync();
            final.Clues.Single().PrepareAnswer();
            return final;
        }
    }
}