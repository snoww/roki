using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        private static readonly Dictionary<string, PokemonData> Data = JsonSerializer.Deserialize<Dictionary<string, PokemonData>>(File.ReadAllText("./data/pokemon.json"), Options);

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
            query = query.SanitizeStringFull();
            return await uow.Context.Pokedex.FirstOrDefaultAsync(p => p.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                .ConfigureAwait(false);
        }

        public async Task<Pokemon> GetPokemonByIdAsync(int number)
        {
            using var uow = _db.GetDbContext();
            return await uow.Context.Pokedex.FirstOrDefaultAsync(p => p.Number == number).ConfigureAwait(false);
        }

        public string GetSprite(string pokemon, int number)
        {
            // temp solution for missing sprites
            string[] sprites;
            if (number > 809 || pokemon.Contains("gmax", StringComparison.OrdinalIgnoreCase) || pokemon.Contains("galar"))
                sprites = Directory.GetFiles("./data/pokemon/gen5", $"{pokemon.Substring(0, 3)}*");
            else
                sprites = Directory.GetFiles("./data/pokemon/ani", $"{pokemon.Substring(0, 3)}*");
            return sprites.FirstOrDefault(path => path.Replace("-", "").Contains(pokemon, StringComparison.OrdinalIgnoreCase));
        }

        public string GetEvolution(Pokemon pokemon)
        {
            using var uow = _db.GetDbContext();
            var pkmn = GetBaseEvo(uow, pokemon);
            return !pkmn.Evolutions.Any() 
                ? "N/A" 
                : GetEvolutionChain(uow, pkmn).Replace(pokemon.Species, Format.Bold(pokemon.Species));
        }

        private static Pokemon GetBaseEvo(IUnitOfWork uow, Pokemon pokemon)
        {
            if (string.IsNullOrEmpty(pokemon.PreEvolution))
                return pokemon;

            Pokemon basic;
            do
            { 
                basic = uow.Context.Pokedex.First(p => p.Name.Equals(pokemon.PreEvolution, StringComparison.OrdinalIgnoreCase));
            } while (!string.IsNullOrEmpty(pokemon.PreEvolution));

            return basic;
        }

        private static string GetEvolutionChain(IUnitOfWork uow, Pokemon pokemon)
        {
            return !pokemon.Evolutions.Any() 
                ? pokemon.Species 
                : pokemon.Evolutions.Aggregate(pokemon.Species, (current, evolution) => current + " > " + GetEvolutionChain(uow, uow.Context.Pokedex.First(p => p.Name.Equals(evolution))));
        }
    }
}