using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services;
using Roki.Core.Services.Database;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
using Roki.Modules.Searches.Common;

namespace Roki.Modules.Searches.Services
{
    public class PokemonService : IRService
    {
        private readonly DbService _db;
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions{PropertyNameCaseInsensitive = true};

        public PokemonService(DbService db)
        {
            _db = db;
        }

        public Color GetColorOfPokemon(string color)
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
            using var uow = _db.GetDbContext();
            query = query.SanitizeStringFull().ToLowerInvariant();
            var pokemon = await uow.Context.Pokedex.FirstOrDefaultAsync(p => p.Name == query)
                .ConfigureAwait(false);
            if (pokemon != null) return pokemon;
            return await GetPokemonByAliasAsync(query).ConfigureAwait(false);
        }
        
        public async Task<Pokemon> GetPokemonByIdAsync(int number)
        {
            using var uow = _db.GetDbContext();
            return await uow.Context.Pokedex.Where(p => p.Number == number).OrderBy(p => p.Name).FirstOrDefaultAsync().ConfigureAwait(false);
        }

        private async Task<Pokemon> GetPokemonByAliasAsync(string query)
        {
            var aliases = JsonDocument.Parse(File.ReadAllText("./data/pokemon_aliases.json"));
            if (!aliases.RootElement.TryGetProperty(query, out var name)) 
                return null;
            
            using var uow = _db.GetDbContext();
            return await uow.Context.Pokedex.FirstOrDefaultAsync(p => p.Species == name.GetString())
                .ConfigureAwait(false);
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
            using var uow = _db.GetDbContext();
            var pkmn = GetBaseEvo(uow, pokemon);
            return pkmn.Evolutions == null
                ? "N/A" 
                : GetEvolutionChain(uow, pkmn);
        }

        private static Pokemon GetBaseEvo(IUnitOfWork uow, Pokemon pokemon)
        {
            if (string.IsNullOrEmpty(pokemon.PreEvolution))
                return pokemon;

            var basic = pokemon;
            do
            { 
                basic = uow.Context.Pokedex.First(p => p.Name == basic.PreEvolution);
            } while (!string.IsNullOrEmpty(basic.PreEvolution));

            return basic;
        }

        private static string GetEvolutionChain(IUnitOfWork uow, Pokemon pokemon)
        {
            if (pokemon.Evolutions == null)
            {
                return pokemon.Species;
            }

            var chain = string.Empty;
            
            chain += $"{pokemon.Species} > {GetEvolutionChain(uow, uow.Context.Pokedex.First(p => p.Name == pokemon.Evolutions.First()))}";
            if (pokemon.Evolutions.Length <= 1) return chain;
            
            foreach (var ev in pokemon.Evolutions.Skip(1))
            {
                var pad = string.Empty;
                if (pokemon.PreEvolution != null)
                    pad += $"{Regex.Replace(pokemon.PreEvolution + pokemon.Species, ".", " ")}   ";
                else
                    pad += Regex.Replace(pokemon.Species, ".", " ");
                chain += $"\n{pad} > {GetEvolutionChain(uow, uow.Context.Pokedex.First(p => p.Name == ev))}";
            }
            
            return chain;
        }

        public async Task<Ability> GetAbilityAsync(string query)
        {
            query = query.SanitizeStringFull().ToLowerInvariant();
            using var uow = _db.GetDbContext();
            return await uow.Context.Abilities.FirstOrDefaultAsync(a => a.Id == query).ConfigureAwait(false);
        }

        public async Task<Move> GetMoveAsync(string query)
        {
            query = query.SanitizeStringFull().ToLowerInvariant();
            using var uow = _db.GetDbContext();
            var move = await uow.Context.Moves.FirstOrDefaultAsync(m => m.Id == query).ConfigureAwait(false);
            if (move != null) return move;
            return await GetMoveAliasAsync(query).ConfigureAwait(false);
        }

        private async Task<Move> GetMoveAliasAsync(string query)
        {
            using var aliases = JsonDocument.Parse(File.ReadAllText("./data/pokemon_move_aliases.json"));
            if (aliases.RootElement.TryGetProperty(query, out var name))
                return null;
            
            using var uow = _db.GetDbContext();
            return await uow.Context.Moves.FirstOrDefaultAsync(p => p.Name == name.GetString()).ConfigureAwait(false);
        }
    }
}