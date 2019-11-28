using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
using Roki.Modules.Searches.Common;
using Roki.Modules.Searches.Services;

namespace Roki.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class PokemonCommands : RokiSubmodule<PokemonService>
        {
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task Pokedex([Leftover] string query = null)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;

                Pokemon pokemon;
                if (int.TryParse(query, out var num))
                    pokemon = await _service.GetPokemonByIdAsync(num).ConfigureAwait(false);
                else
                    pokemon = await _service.GetPokemonByNameAsync(query).ConfigureAwait(false);

                if (pokemon == null)
                {
                    await ctx.Channel.SendErrorAsync("No Pokémon of that name/id found.").ConfigureAwait(false);
                    return;
                }

                var types = pokemon.Type0 + (string.IsNullOrEmpty(pokemon.Type1) ? string.Empty : $", {pokemon.Type1}");
                var abilities = pokemon.Ability0 + (string.IsNullOrEmpty(pokemon.Ability1) ? string.Empty : $", {pokemon.Ability1}") +
                                (string.IsNullOrEmpty(pokemon.AbilityH) ? string.Empty : $", {Format.Italics(pokemon.AbilityH)}");
                var baseStats = $"`HP: {pokemon.Hp} Atk: {pokemon.Attack} Def: {pokemon.Defence} Spa: {pokemon.SpecialAttack} " +
                                $"Spd: {pokemon.SpecialDefense} Spe {pokemon.Speed}`";
                
                var embed = new EmbedBuilder().WithColor(_service.GetColorOfPokemon(pokemon.Color))
                    .WithTitle($"#{pokemon.Number:D3} {pokemon.Species}")
                    .AddField("Types", types, true)
                    .AddField("Abilities", abilities, true)
                    .AddField("Base Stats", baseStats, true)
                    .AddField("Height", $"{pokemon.Height:N1} m", true)
                    .AddField("Weight", $"{pokemon.Weight} kg", true)
                    .AddField("Egg Groups", string.Join(", ", pokemon.EggGroups));

                if (pokemon.Number > 809 || pokemon.Name.Contains("gmax", StringComparison.OrdinalIgnoreCase) ||
                    pokemon.Name.Contains("galar", StringComparison.OrdinalIgnoreCase))
                {
                    var sprite = await _service.GetGen8SpriteAsync(pokemon.Name).ConfigureAwait(false);
                    embed.WithThumbnailUrl($"attachment://{sprite.Split("/").Last()}");
//
//                    var embed = new EmbedBuilder()
//                        .WithColor(_service.GetColorOfPokemon(species.Color.Name))
//                        .WithTitle($"#{pokemon.Id} {data.Name}")
//                        .WithDescription($"{species.Genera[2].Genus}")
//                        .WithThumbnailUrl("https://play.pokemonshowdown.com/sprites/xyani/" + data.Sprite + ".gif")
//                        .AddField("Types", string.Join(", ", pokemon.Types.OrderBy(t => t.Slot).Select(t => t.Type.Name.ToTitleCase()).ToList()), true)
//                        .AddField("Abilities", string.Join(", ", 
//                            pokemon.Abilities.OrderBy(a => a.Slot).Select(a =>
//                            {
//                                if (a.IsHidden)
//                                    return "*" + a.Ability.Name.ToTitleCase().Replace('-', ' ') + "*";
//                                return a.Ability.Name.ToTitleCase().Replace('-', ' ');
//                            }).ToList()), true)
//                        .AddField("Base Stats", $"`HP: {pokemon.Stats[5].BaseStat} Atk: {pokemon.Stats[4].BaseStat} Def: {pokemon.Stats[3].BaseStat} Sp. Atk: {pokemon.Stats[2].BaseStat} Sp. Def: {pokemon.Stats[1].BaseStat} Speed: {pokemon.Stats[0].BaseStat}`")
//                        .AddField("Height", $"{(double) pokemon.Height / 10} m", true)
//                        .AddField("Weight", $"{(double) pokemon.Weight / 10} kg", true)
//                        .AddField("Evolution", _service.GetPokemonEvolutionChain(pokemon.Name, evoChain));
//                    
//                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Ability([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;
                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

//                try
//                {
//                    var ability = await _pokeClient.GetResourceAsync<Ability>(query).ConfigureAwait(false);
//                    
//                    var embed = new EmbedBuilder().WithOkColor()
//                        .WithTitle(ability.Name.ToTitleCase().Replace('-', ' '))
//                        .WithDescription(ability.EffectEntries[0].Effect)
//                        .AddField("Introduced In", $"Generation {ability.Generation.Name.Split('-')[1].ToUpperInvariant()}");
//                    
//                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
//                }
//                catch
//                {
//                    await ctx.Channel.SendErrorAsync("No ability of that name found.").ConfigureAwait(false);
//                }
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Move([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;
//                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
//                try
//                {
//                    var move = await _pokeClient.GetResourceAsync<Move>(query).ConfigureAwait(false);
//
//                    var embed = new EmbedBuilder().WithOkColor()
//                        .WithTitle(move.Name.ToTitleCase().Replace('-', ' '))
//                        .WithDescription(move.EffectChanges != null
//                            ? move.EffectEntries[0].Effect.Replace("$effect_chance", move.EffectChance.ToString())
//                            : move.EffectEntries[0].Effect)
//                        .AddField("Type", move.Type.Name.ToTitleCase(), true)
//                        .AddField("Damage Type", move.DamageClass.Name.ToTitleCase(), true)
//                        .AddField("Accuracy", move.Accuracy != null ? $"{move.Accuracy}%" : "—", true)
//                        .AddField("Power", move.Power != null ? $"{move.Power}" : "—", true)
//                        .AddField("PP", move.Pp, true)
//                        .AddField("Priority", move.Priority, true)
//                        .AddField("Introduced In", $"Generation {move.Generation.Name.Split('-')[1].ToUpperInvariant()}", true);
//
//                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Nature([Leftover] string query)
            {
//                if (string.IsNullOrWhiteSpace(query))
//                    return;
//                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
//                try
//                {
//                    var nature = await _pokeClient.GetResourceAsync<Nature>(query).ConfigureAwait(false);
//                    
//                    var embed = new EmbedBuilder().WithOkColor()
//                        .WithTitle(nature.Name.ToTitleCase());
//                    
//                    // checks if nature is not neutral
//                    if (nature.IncreasedStat != null)    
//                        embed.AddField("Increased Stat", nature.IncreasedStat.Name.ToTitleCase().Replace('-', ' '), true)
//                        .AddField("Decreased Stat", nature.DecreasedStat.Name.ToTitleCase().Replace('-', ' '), true)
//                        .AddField("Likes Flavor", nature.LikesFlavor.Name.ToTitleCase(), true)
//                        .AddField("Hates Flavor", nature.HatesFlavor.Name.ToTitleCase(), true);
//                    else
//                        embed.AddField("Increased Stat", "—", true)
//                            .AddField("Decreased Stat", "—", true)
//                            .AddField("Likes Flavor", "—", true)
//                            .AddField("Hates Flavor", "—", true);
//                    
//                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
//                }
//                catch
//                {
//                    await ctx.Channel.SendErrorAsync("Nature not found.").ConfigureAwait(false);
//                }
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Item([Leftover] string query)
            {
//                if (string.IsNullOrWhiteSpace(query))
//                    return;
//                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
//                try
//                {
//                    var item = await _pokeClient.GetResourceAsync<Item>(query).ConfigureAwait(false);
//
//                    var embed = new EmbedBuilder().WithOkColor()
//                        .WithAuthor(item.Name.ToTitleCase().Replace('-', ' '))
//                        .WithDescription(item.EffectEntries[0].Effect)
//                        .WithThumbnailUrl(item.Sprites.Default)
//                        .AddField("Category", item.Category.Name.ToTitleCase().Replace('-', ' '), true)
//                        .AddField("Cost", item.Cost, true);
//                    if (item.FlingPower != null)
//                        embed.AddField("Fling Power", item.FlingPower, true);
//                    
//                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
//                }
//                catch
//                {
//                    await ctx.Channel.SendErrorAsync("Item not found.").ConfigureAwait(false);
//                }
            }
        }
    }
}