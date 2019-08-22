using System;
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

                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

                try
                {
                    var pokemon = await _pokeClient.GetResourceAsync<Pokemon>(query).ConfigureAwait(false);
                    var species = await _pokeClient.GetResourceAsync(pokemon.Species).ConfigureAwait(false);
                    var evoChain = await _pokeClient.GetResourceAsync(species.EvolutionChain).ConfigureAwait(false);

                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle($"#{pokemon.Id} {pokemon.Name.ToTitleCase()}")
                        .WithDescription($"{species.Genera[2].Genus}")
                        .WithThumbnailUrl(pokemon.Sprites.FrontDefault)
                        .AddField("Types", string.Join("\n", pokemon.Types.OrderBy(t => t.Slot).Select(t => t.Type.Name.ToTitleCase()).ToList()), true)
                        .AddField("Abilities", string.Join("\n", pokemon.Abilities.OrderBy(a => a.Slot).Select(a => a.Ability.Name.ToTitleCase().Replace('-', ' ')).ToList()), true)
                        .AddField("Base Stats", $"`HP: {pokemon.Stats[5].BaseStat}\nAttack: {pokemon.Stats[4].BaseStat}\nSp. Atk: {pokemon.Stats[2].BaseStat}\nDefense: {pokemon.Stats[3].BaseStat}\nSp. Def: {pokemon.Stats[1].BaseStat}\nSpeed: {pokemon.Stats[0].BaseStat}\n`", true);

                    if (evoChain.Chain.EvolvesTo.Count > 0)
                    {
                        var evostr = "";
                        evostr += evoChain.Chain.Species.Name.ToTitleCase() + "\n";
                        foreach (var evo in evoChain.Chain.EvolvesTo)
                        {
                            evostr += evo.Species.Name.ToTitleCase() + "\n";
                            if (evo.EvolvesTo.Count > 0)
                            {
                                evostr += string.Join("\n", evo.EvolvesTo.Select(e => e.Species.Name.ToTitleCase()).ToList());
                            }
                        }

                        embed.AddField("Evolution", evostr, true);
                    }
                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync("No Pokémon of that name found.");
                }
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Ability([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;
                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

                try
                {
                    var ability = await _pokeClient.GetResourceAsync<Ability>(query).ConfigureAwait(false);
                    
                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle(ability.Name.ToTitleCase().Replace('-', ' '))
                        .WithDescription(ability.EffectEntries[0].Effect)
                        .AddField("Introduced In", $"Generation {ability.Generation.Name.Split('-')[1].ToUpperInvariant()}");
                    
                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync("No ability of that name found.").ConfigureAwait(false);
                }
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Move([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;
                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                try
                {
                    var move = _pokeClient.GetResourceAsync<Move>(query).Result;

                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle(move.Name.ToTitleCase().Replace('-', ' '))
                        .WithDescription(move.EffectChanges != null
                            ? move.EffectEntries[0].Effect.Replace("$effect_chance", move.EffectChance.ToString())
                            : move.EffectEntries[0].Effect)
                        .AddField("Type", move.Type.Name.ToTitleCase(), true)
                        .AddField("Damage Type", move.DamageClass.Name.ToTitleCase(), true)
                        .AddField("Accuracy", $"{move.Accuracy}%", true);

                    if (move.Power != null)
                        embed.AddField("Power", move.Power, true);
                    else
                        embed.AddField("Power", "—", true);

                    embed.AddField("PP", move.Pp, true)
                        .AddField("Priority", move.Priority, true)
                        .AddField("Introduced In", $"Generation {move.Generation.Name.Split('-')[1].ToUpperInvariant()}", true);

                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync("Move not found.").ConfigureAwait(false);
                }
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Nature([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;
                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                try
                {
                    var nature = await _pokeClient.GetResourceAsync<Nature>(query);
                    
                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle(nature.Name.ToTitleCase());
                    
                    // checks if nature is not neutral
                    if (nature.IncreasedStat != null)    
                        embed.AddField("Increased Stat", nature.IncreasedStat.Name.ToTitleCase(), true)
                        .AddField("Decreased Stat", nature.DecreasedStat.Name.ToTitleCase(), true)
                        .AddField("Likes Flavor", nature.LikesFlavor.Name, true)
                        .AddField("Hates Flavor", nature.HatesFlavor.Name, true);
                    else
                        embed.AddField("Increased Stat", "—", true)
                            .AddField("Decreased Stat", "—", true)
                            .AddField("Likes Flavor", "—", true)
                            .AddField("Hates Flavor", "—", true);
                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync("Nature not found.").ConfigureAwait(false);
                }
            }
        }
    }
}