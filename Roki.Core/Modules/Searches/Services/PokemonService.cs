using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Roki.Extensions;
using Roki.Modules.Searches.Models;
using Roki.Services;
using Roki.Services.Database.Models;

namespace Roki.Modules.Searches.Services
{
    /*public class PokemonService : IRokiService
    {
        private static readonly List<Pokemon> Pokedex = JsonSerializer.Deserialize<List<Pokemon>>("./data/pokemon/pokedex.json");
        private static readonly List<Ability> Abilities = JsonSerializer.Deserialize<List<Ability>>("./data/pokemon/abilities.json");
        private static readonly List<Move> Moves = JsonSerializer.Deserialize<List<Move>>("./data/pokemon/move.json");

        public PokemonService()
        {
        }

        public static Color GetColorOfPokemon(string color)
        {
            color = color.ToLower();
            switch (color)
            {
                case "black":
                    return new Color(0x000000);
                case "blue":
                    return new Color(0x257CFF);
                case "brown":
                    return new Color(0xA3501A);
                case "gray":
                    return new Color(0x808080);
                case "green":
                    return new Color(0x008000);
                case "pink":
                    return new Color(0xFF65A5);
                case "purple":
                    return new Color(0xA63DE8);
                case "red":
                    return new Color(0xFF3232);
                case "white":
                    return new Color(0xFFFFFF);
                case "yellow":
                    return new Color(0xFFF359);
                default:
                    return new Color(0x000000);
            }
        }

        public static async Task<Pokemon> GetPokemonByNameAsync(string query)
        {
            query = query.SanitizeStringFull().ToLowerInvariant();
            Pokemon pokemon = Pokedex.FirstOrDefault(x => x.Id == query);
            if (pokemon != null) 
                return pokemon;
            
            using JsonDocument aliases = JsonDocument.Parse(await File.ReadAllTextAsync("./data/pokemon_aliases.json"));
            if (!aliases.RootElement.TryGetProperty(query, out JsonElement name)) 
                return null;
            return Pokedex.FirstOrDefault(x => x.Id == name.GetString());
        }
        
        public static Pokemon GetPokemonByIdAsync(int number)
        {
            return Pokedex.FirstOrDefault(x => x.Num == number);
        }

        public static string GetSprite(string pokemon, int number)
        {
            // temp solution for missing sprites
            string[] sprites;
            if (number > 809 || pokemon.Contains("gmax", StringComparison.OrdinalIgnoreCase) || pokemon.Contains("galar"))
                sprites = Directory.GetFiles("./data/pokemon/gen5", $"{pokemon.Substring(0, 3)}*");
            else
                sprites = Directory.GetFiles("./data/pokemon/ani", $"{pokemon.Substring(0, 3)}*");
            return sprites.First(path => path.Replace("-", "").Replace(".gif", "").Replace(".png", "")
                .EndsWith(pokemon, StringComparison.OrdinalIgnoreCase));
        }

        public string GetEvolution(Pokemon pokemon)
        {
            Pokemon baseEvo = GetBaseEvo(pokemon);
            return baseEvo.Evos == null
                ? "N/A" 
                : GetEvolutionChain(baseEvo);
        }

        private static Pokemon GetBaseEvo(Pokemon pokemon)
        {
            if (string.IsNullOrEmpty(pokemon.Prevo))
                return pokemon;

            Pokemon basic = pokemon;
            do
            {
                basic = Pokedex.First(x => x.Id == basic.Prevo);
            } while (!string.IsNullOrEmpty(basic.Prevo));

            return basic;
        }

        private static string GetEvolutionChain(Pokemon pokemon)
        {
            if (pokemon.Evos == null)
            {
                return pokemon.Species;
            }

            var chain = new StringBuilder();

            chain.Append($"{pokemon.Species} > {GetEvolutionChain(Pokedex.First(x => x.Id == pokemon.Evos.First()))}");
            if (pokemon.Evos.Count <= 1) 
                return chain.ToString();
            
            foreach (string ev in pokemon.Evos.Skip(1))
            {
                var pad = new StringBuilder();
                if (pokemon.Evos != null)
                    pad.Append($"{Regex.Replace(pokemon.Prevo + pokemon.Species, ".", " ")}");
                else
                    pad.Append(Regex.Replace(pokemon.Species, ".", " "));
                chain.Append($"\n{pad} > {GetEvolutionChain(Pokedex.First(x => x.Id == ev))}");
            }
            
            return chain.ToString();
        }

        public static string FormatStats(Pokemon pokemon)
        {
            var stats = new[] 
            {
                pokemon.BaseStats["hp"], pokemon.BaseStats["atk"], pokemon.BaseStats["def"], 
                pokemon.BaseStats["spa"], pokemon.BaseStats["spd"], pokemon.BaseStats["spe"]
            };
            
            var max = (int) Math.Ceiling(Math.Log10(stats.Max()));

            return max == 2 
                ? $"```css\nHP:  {stats[0],2}  Atk: {stats[1],2}  Def: {stats[2],2}\nSpA: {stats[3],2}  SpD: {stats[4],2}  Spd: {stats[5],2}```" 
                : $"```css\nHP:  {stats[0],3}  Atk: {stats[1],3}  Def: {stats[2],3}\nSpA: {stats[3],3}  SpD: {stats[4],3}  Spd: {stats[5],3}```";
        }

        public Ability GetAbilityAsync(string query)
        {
            query = query.SanitizeStringFull().ToLowerInvariant();
            return Abilities.FirstOrDefault(x => x.Name == query);
        }

        public static async Task<Move> GetMoveAsync(string query)
        {
            query = query.SanitizeStringFull().ToLowerInvariant();
            Move move = Moves.FirstOrDefault(x => x.Id == query);
            if (move != null) 
                return move;
            
            using JsonDocument aliases = JsonDocument.Parse(await File.ReadAllTextAsync("./data/pokemon_move_aliases.json"));
            if (!aliases.RootElement.TryGetProperty(query, out JsonElement name))
                return null;
            
            return Moves.FirstOrDefault(x => x.Id == name.GetString());
        }
    }*/
}