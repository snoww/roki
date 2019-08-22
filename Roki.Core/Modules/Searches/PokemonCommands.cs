using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PokeApiNet;
using PokeApiNet.Models;
using Roki.Common.Attributes;
using Roki.Core.Extentions;
using Roki.Extensions;

namespace Roki.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class PokemonCommands : RokiSubmodule
        {
            private readonly PokeApiClient _pokeClient = new PokeApiClient();

            [RokiCommand, Usage, Description, Aliases]
            public async Task Pokemon([Leftover] string query = null)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;

                var pokemon = await _pokeClient.GetResourceAsync<Pokemon>(query).ConfigureAwait(false);
                if (pokemon == null)
                {
                    await ctx.Channel.SendErrorAsync("No Pokémon of that name found.");
                    return;
                }

                var species = await _pokeClient.GetResourceAsync(pokemon.Species).ConfigureAwait(false);
                var evoChain = await _pokeClient.GetResourceAsync(species.EvolutionChain).ConfigureAwait(false);

                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle($"#{pokemon.Id} {pokemon.Name.ToTitleCase()}")
                    .WithDescription($"{species.Genera[2].Genus}")
                    .WithThumbnailUrl(pokemon.Sprites.FrontDefault)
                    .AddField("Types", string.Join("\n", pokemon.Types.OrderBy(t => t.Slot).Select(t => t.Type.Name.ToTitleCase()).ToList()), true)
                    .AddField("Abilities",
                        string.Join("\n",
                            pokemon.Abilities.OrderBy(a => a.Slot).Select(a => a.Ability.Name.ToTitleCase().Replace('-', ' ')).ToList()), true);

                if (evoChain != null)
                {
                    var evostr = "";
                    evostr += evoChain.Chain.Species.Name + "\n";
                    foreach (var evo in evoChain.Chain.EvolvesTo)
                    {
                        evostr += evo.Species.Name.ToTitleCase() + "\n";
                        if (evo.EvolvesTo != null) evostr += evo.EvolvesTo.Select(e => e.Species.Name) + "\n";
                    }

                    embed.AddField("Evolution", evostr, true);
                }

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }
    }
}