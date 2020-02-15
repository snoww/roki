using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Searches.Services
{
    public class PokemonService : IRokiService
    {
        private readonly IMongoService _mongo;

        public PokemonService(IMongoService mongo)
        {
            _mongo = mongo;
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

        public async Task<Pokemon> GetPokemonByNameAsync(string query)
        {
            query = query.SanitizeStringFull().ToLower();
            var pokemon = await _mongo.Context.GetPokemonAsync(query);
            if (pokemon != null) 
                return pokemon;
            return await GetPokemonByAliasAsync(query).ConfigureAwait(false);
        }
        
        public async Task<Pokemon> GetPokemonByIdAsync(int number)
        {
            return await _mongo.Context.GetPokemonByNumberAsync(number).ConfigureAwait(false);
        }

        private async Task<Pokemon> GetPokemonByAliasAsync(string query)
        {
            var aliases = JsonDocument.Parse(File.ReadAllText("./data/pokemon_aliases.json"));
            if (!aliases.RootElement.TryGetProperty(query, out var name)) 
                return null;

            return await _mongo.Context.GetPokemonAsync(name.GetString()).ConfigureAwait(false);
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

        public async Task<string> GetEvolution(Pokemon pokemon)
        {
            var pkmn = await GetBaseEvo(pokemon).ConfigureAwait(false);
            return pkmn.Evos == null
                ? "N/A" 
                : await GetEvolutionChain(pkmn).ConfigureAwait(false);
        }

        private async Task<Pokemon> GetBaseEvo(Pokemon pokemon)
        {
            if (string.IsNullOrEmpty(pokemon.Prevo))
                return pokemon;

            var basic = pokemon;
            do
            {
                basic = await _mongo.Context.GetPokemonAsync(basic.Prevo).ConfigureAwait(false);
            } while (!string.IsNullOrEmpty(basic.Prevo));

            return basic;
        }

        private async Task<string> GetEvolutionChain(Pokemon pokemon)
        {
            if (pokemon.Evos == null)
            {
                return pokemon.Species;
            }

            var chain = new StringBuilder();

            chain.Append($"{pokemon.Species} > {await GetEvolutionChain(await _mongo.Context.GetPokemonAsync(pokemon.Evos.First()))}");
            if (pokemon.Evos.Count <= 1) 
                return chain.ToString();
            
            foreach (var ev in pokemon.Evos.Skip(1))
            {
                var pad = new StringBuilder();
                if (pokemon.Evos != null)
                    pad.Append($"{Regex.Replace(pokemon.Prevo + pokemon.Species, ".", " ")}");
                else
                    pad.Append(Regex.Replace(pokemon.Species, ".", " "));
                chain.Append($"\n{pad} > {await GetEvolutionChain(await _mongo.Context.GetPokemonAsync(ev))}");
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

        public async Task<Ability> GetAbilityAsync(string query)
        {
            query = query.SanitizeStringFull().ToLower();
            return await _mongo.Context.GetAbilityAsync(query).ConfigureAwait(false);
        }

        public async Task<Move> GetMoveAsync(string query)
        {
            query = query.SanitizeStringFull().ToLower();
            var move = await _mongo.Context.GetMoveAsync(query).ConfigureAwait(false);
            if (move != null) 
                return move;
            return await GetMoveAliasAsync(query).ConfigureAwait(false);
        }

        private async Task<Move> GetMoveAliasAsync(string query)
        {
            using var aliases = JsonDocument.Parse(File.ReadAllText("./data/pokemon_move_aliases.json"));
            if (!aliases.RootElement.TryGetProperty(query, out var name))
                return null;
            
            return await _mongo.Context.GetMoveAsync(name.GetString()).ConfigureAwait(false);
        }

        public async Task<PokemonItem> GetItemAsync(string query)
        {
            query = query.SanitizeStringFull().ToLower();
            return await _mongo.Context.GetItemAsync(query).ConfigureAwait(false);
        }
    }
}