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
using Roki.Modules.Searches.Common;

namespace Roki.Modules.Games.Services
{
    public class ShowdownService : IRService
    {
        public readonly ConcurrentDictionary<ulong, string> Games = new ConcurrentDictionary<ulong, string>();
        public readonly ConcurrentDictionary<string, string> TeamsAndId = new ConcurrentDictionary<string, string>();
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions{PropertyNameCaseInsensitive = true};
        private static readonly Dictionary<string, PokemonData> Data = JsonSerializer.Deserialize<Dictionary<string, PokemonData>>(File.ReadAllText("./data/pokemon.json"), Options);
        
        public ShowdownService()
        {
        }

        public async Task ConfigureAiGameAsync(string generation)
        {
            var env = await File.ReadAllTextAsync("/home/snow/Documents/showdown/.env").ConfigureAwait(false);
            var env2 = await File.ReadAllTextAsync("/home/snow/Documents/showdown2/.env").ConfigureAwait(false);
            env = Regex.Replace(env, @"gen\d", $"gen{generation}");
            env2 = Regex.Replace(env2, @"gen\d", $"gen{generation}");
            await File.WriteAllTextAsync("/home/snow/Documents/showdown/.env", env).ConfigureAwait(false);
            await File.WriteAllTextAsync("/home/snow/Documents/showdown2/.env", env2).ConfigureAwait(false);
        }

        public async void RunAiGameAsync(string generation, string uid)
        {
            using var proc = new Process {StartInfo = {FileName = "./scripts/ai.sh", UseShellExecute = false, RedirectStandardOutput = true}};
            proc.Start();
            var reader = proc.StandardOutput;
            var gameId = "";
            var gameIdReceived = false;
            while (TeamsAndId.Count < 3)
            {
                var output = await reader.ReadLineAsync().ConfigureAwait(false);
                if (output.StartsWith("|request|{", StringComparison.OrdinalIgnoreCase))
                {
                    var (p, team) = ParseTeamAsync(output);
                    TeamsAndId.TryAdd(uid + p, team);
                }
                else if (!gameIdReceived && output.Contains("battle-gen", StringComparison.OrdinalIgnoreCase))
                {
                    gameId = output.Substring(output.IndexOf("battle-gen", StringComparison.OrdinalIgnoreCase), 34);
                    gameIdReceived = true;
                }
            }
            proc.WaitForExit();
            File.AppendAllText(@"./data/pokemon-logs/battle-logs", $"{uid}={gameId}\n"); 
        }

        public async Task<int> GetWinnerAsync(string uid)
        {
            var gameId = await GetBetPokemonGame(uid).ConfigureAwait(false);
            var p1 = await File.ReadAllLinesAsync($@"./logs/1-{gameId}.log").ConfigureAwait(false);
            var winner = p1.Any(l => l.StartsWith("|win|rokibot1", StringComparison.OrdinalIgnoreCase)) ? 2 : 1;
            File.Delete($@"./logs/1-{gameId}.log");
            File.Delete($@"./logs/rokibot-{gameId}.log");
            return winner;
        }

        private (string, string) ParseTeamAsync(string rawTeam)
        {
            var json = rawTeam.Substring(rawTeam.IndexOf("{", StringComparison.OrdinalIgnoreCase));
            using var team = JsonDocument.Parse(json);
            var teamStr = new StringBuilder();
            foreach (var pokemon in team.RootElement.GetProperty("side").GetProperty("pokemon").EnumerateArray())
            {
                teamStr.Append(pokemon.GetProperty("details").GetString() + "\n");
            }
            var player = team.RootElement.GetProperty("side").GetProperty("id").GetString();
            return (player, string.Join("\n", teamStr));
        }

        public string GetPokemonSprite(string query)
        {
            query = query.Split(',').First().ToLower().Replace(" ", "-", StringComparison.Ordinal).Replace("%", "", StringComparison.Ordinal)
                .Replace(".", "", StringComparison.Ordinal).Replace(":", "", StringComparison.Ordinal);
            var poke = query.EndsWith("-*", StringComparison.Ordinal) ? Data[query.Replace("-*", "", StringComparison.Ordinal)] : Data[query];

            return poke.Sprite;
        }

        public async Task<string> GetBetPokemonGame(string uid)
        {
            var logs = await File.ReadAllLinesAsync(@"./data/pokemon-logs/battle-logs").ConfigureAwait(false);
            return (from log in logs where log.StartsWith(uid, StringComparison.OrdinalIgnoreCase) select log.Substring(9)).FirstOrDefault();
        }
    }
}