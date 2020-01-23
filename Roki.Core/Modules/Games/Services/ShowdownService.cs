using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Games.Common;
using Roki.Modules.Searches.Models;

namespace Roki.Modules.Games.Services
{
    public class ShowdownService : IRokiService
    {
        public readonly ConcurrentDictionary<ulong, Showdown> ActiveGames = new ConcurrentDictionary<ulong, Showdown>();
        
        public static async Task<string> GetBetPokemonGame(string uid)
        {
            var logs = await File.ReadAllLinesAsync(@"./data/pokemon-logs/battle-logs").ConfigureAwait(false);
            return (from log in logs where log.StartsWith(uid, StringComparison.OrdinalIgnoreCase) select log.Substring(9)).FirstOrDefault();
        }
    }
}