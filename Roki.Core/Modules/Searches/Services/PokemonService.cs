using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services;
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

        public async Task<string> GetGen8SpriteAsync(string pokemon, int number)
        {
            // temp solution for missing sprites
            string[] sprites;
            if (number > 809 || pokemon.Contains("gmax", StringComparison.OrdinalIgnoreCase) || pokemon.Contains("galar"))
                sprites = Directory.GetFiles("./data/pokemon/gen5", $"{pokemon.Substring(0, 3)}*");
            else
                sprites = Directory.GetFiles("./data/pokemon/ani", $"{pokemon.Substring(0, 3)}*");
            return sprites.FirstOrDefault(path => path.Replace("-", "").Contains(pokemon, StringComparison.OrdinalIgnoreCase));
        }
        
//        public string GetPokemonEvolutionChain(string pokemon, EvolutionChain evoChain)
//        {
//            // hardcode wurmple?
//            if (evoChain.Chain.EvolvesTo.Count < 1)
//                return "No Evolutions";
//            var evoStr = "";
//            evoStr += pokemon == evoChain.Chain.Species.Name
//                ? $"**{evoChain.Chain.Species.Name.ToTitleCase()}** > "
//                : $"{evoChain.Chain.Species.Name.ToTitleCase()} > ";
//
//            foreach (var evo in evoChain.Chain.EvolvesTo)
//            {
//                evoStr += pokemon == evo.Species.Name
//                    ? $"**{evo.Species.Name.ToTitleCase()}**"
//                    : $"{evo.Species.Name.ToTitleCase()}";
//                if (evo.EvolvesTo.Count <= 0)
//                {
//                    evoStr += ", "; 
//                    continue;
//                }
//
//                evoStr += evo.EvolvesTo.Aggregate("", (current, link) => current + (pokemon == link.Species.Name ? $" > **{link.Species.Name.ToTitleCase()}**, " : $" > {link.Species.Name.ToTitleCase()}"));
//            }
//            
//            return evoStr.TrimEnd(',', ' ');
//        }
    }
}