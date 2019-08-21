using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PokeApiNet;
using PokeApiNet.Models;
using Roki.Common.Attributes;
using Roki.Extensions;

namespace Roki.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class PokemonCommands : RokiSubmodule
        {
            private readonly PokeApiClient _pokeClient;

            public PokemonCommands(PokeApiClient pokeClient)
            {
                _pokeClient = pokeClient;
            }
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task Pokemon([Leftover] string query = null)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;

                var pokemon = await _pokeClient.GetResourceAsync<Pokemon>(query).ConfigureAwait(false);
                if (pokemon == null)
                    return;
                var species = await _pokeClient.GetResourceAsync(pokemon.Species).ConfigureAwait(false);

                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle($"{species.Id} {species.Name}")
                    .WithDescription($"{species.Genera[0].Genus}")
                    .WithThumbnailUrl(pokemon.Sprites.FrontDefault);

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }
    }
}