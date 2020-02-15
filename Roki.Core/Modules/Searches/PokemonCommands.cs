using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Searches.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class PokemonCommands : RokiSubmodule<PokemonService>
        {
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task Pokedex([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;

                Pokemon pokemon;
                if (int.TryParse(query, out var num))
                    pokemon = await Service.GetPokemonByIdAsync(num).ConfigureAwait(false);
                else
                    pokemon = await Service.GetPokemonByNameAsync(query).ConfigureAwait(false);

                if (pokemon == null)
                {
                    await Context.Channel.SendErrorAsync("No Pokémon of that name/id found.").ConfigureAwait(false);
                    return;
                }

                var types = string.Join("\n", pokemon.Types);
                var abilities = string.Join("\n", pokemon.Abilities.Select(x => x.Key == "H" ? Format.Italics(x.Value) : x.Value));

                var embed = new EmbedBuilder().WithColor(PokemonService.GetColorOfPokemon(pokemon.Color))
                    .WithTitle($"#{pokemon.Num:D3} {pokemon.Species}")
                    .AddField("Types", types, true)
                    .AddField("Abilities", abilities, true);
                
                if (pokemon.GenderRatio != null)
                    embed.AddField("Gender Ratio", $"```{string.Join("\n", pokemon.GenderRatio.Select(x => $"{x.Key}: {x.Value:P1}"))}```", true);

                embed.AddField("Base Stats", PokemonService.FormatStats(pokemon))
                    .AddField("Height", $"{pokemon.Height:N1} m", true)
                    .AddField("Weight", $"{pokemon.Weight} kg", true)
                    .AddField("Egg Groups", string.Join("\n", pokemon.EggGroups), true)
                    .AddField("Evolution", $"```php\n{await Service.GetEvolution(pokemon).ConfigureAwait(false)}```");

                var sprite = PokemonService.GetSprite(pokemon.Id, pokemon.Num);
                embed.WithThumbnailUrl($"attachment://{sprite.Split("/").Last()}");

                await Context.Channel.SendFileAsync(sprite, embed: embed.Build()).ConfigureAwait(false);
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Ability([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;
                await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

                var ability = await Service.GetAbilityAsync(query).ConfigureAwait(false);
                if (ability == null)
                {
                    await Context.Channel.SendErrorAsync("No ability of that name found.").ConfigureAwait(false);
                    return;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle(ability.Name)
                        .WithDescription((ability.Desc ?? ability.ShortDesc).TrimTo(2048))
                        .AddField("Rating", $"{ability.Rating:N1}"))
                    .ConfigureAwait(false);
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Move([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;
                await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
                var move = await Service.GetMoveAsync(query).ConfigureAwait(false);
                if (move == null)
                {
                    await Context.Channel.SendErrorAsync("No move of that name found.").ConfigureAwait(false);
                    return;
                }
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle(move.Name)
                        .WithDescription((move.Desc ?? move.ShortDesc).TrimTo(2048))
                        .AddField("Type", move.Type, true)
                        .AddField("Category", move.Category, true)
                        .AddField("Accuracy", move.Accuracy != null ? $"{move.Accuracy.Value}" : "-", true)
                        .AddField("Power", move.Power, true)
                        .AddField("PP", move.Pp, true)
                        .AddField("Priority", move.Priority, true))
                    .ConfigureAwait(false);
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Item([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;
                await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
                var item = await Service.GetItemAsync(query);

                if (item == null)
                {
                    await Context.Channel.SendErrorAsync("Item not found.").ConfigureAwait(false);
                    return;
                }
                
                var embed = new EmbedBuilder().WithDynamicColor(Context)
                    .WithAuthor(item.Name)
                    .WithDescription(item.Desc)
                    .AddField("Introduced in", $"Generation {item.Gen}", true);
                if (item.Fling != null)
                    embed.AddField("Fling Power", string.Join("\n", item.Fling.Select(x => x.Key == "basePower" ? $"Base Power: `{x.Value}`" : $"{x.Key}: `{x.Value}`")), true);
                    
                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }
    }
}