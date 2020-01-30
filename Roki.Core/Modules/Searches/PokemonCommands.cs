using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Searches.Services;
using Roki.Services.Database.Data;

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
                    pokemon = await Service.GetPokemonByIdAsync(num).ConfigureAwait(false);
                else
                    pokemon = await Service.GetPokemonByNameAsync(query).ConfigureAwait(false);

                if (pokemon == null)
                {
                    await Context.Channel.SendErrorAsync("No Pokémon of that name/id found.").ConfigureAwait(false);
                    return;
                }

                var types = pokemon.Type0 + (string.IsNullOrEmpty(pokemon.Type1) ? string.Empty : $", {pokemon.Type1}");
                var abilities = pokemon.Ability0 + (string.IsNullOrEmpty(pokemon.Ability1) ? string.Empty : $", {pokemon.Ability1}") +
                                (string.IsNullOrEmpty(pokemon.AbilityH) ? string.Empty : $", {Format.Italics(pokemon.AbilityH)}");
                var baseStats = $"`HP:  {pokemon.Hp}|Atk: {pokemon.Attack}|Def: {pokemon.Defence}\nSpa: {pokemon.SpecialAttack}" +
                                $"|Spd: {pokemon.SpecialDefense}|Spe: {pokemon.Speed}`";
                
                var embed = new EmbedBuilder().WithColor(Service.GetColorOfPokemon(pokemon.Color))
                    .WithTitle($"#{pokemon.Number:D3} {pokemon.Species}")
                    .AddField("Types", types, true)
                    .AddField("Abilities", abilities, true)
                    .AddField("Base Stats", baseStats, true)
                    .AddField("Height", $"{pokemon.Height:N1} m", true)
                    .AddField("Weight", $"{pokemon.Weight} kg", true)
                    .AddField("Evolution", Format.Code(Service.GetEvolution(pokemon)))
                    .AddField("Egg Groups", string.Join(", ", pokemon.EggGroups), true);

                if (pokemon.MaleRatio.HasValue)
                    embed.AddField("Gender Ratio", $"M: `{pokemon.MaleRatio.Value:P1}` F: `{pokemon.FemaleRatio:P1}`", true);
                
                var sprite = PokemonService.GetSprite(pokemon.Name, pokemon.Number);
                embed.WithThumbnailUrl($"attachment://{sprite.Split("/").Last()}");

                await Context.Channel.SendFileAsync(sprite, embed: embed.Build()).ConfigureAwait(false);
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Ability([Leftover] string query = null)
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

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle(ability.Name)
                        .WithDescription((ability.Description ?? ability.ShortDescription).TrimTo(2048))
                        .AddField("Rating", $"{ability.Rating:N1}"))
                    .ConfigureAwait(false);
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Move([Leftover] string query = null)
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
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle(move.Name)
                        .WithDescription((move.Description ?? move.ShortDescription).TrimTo(2048))
                        .AddField("Type", move.Type, true)
                        .AddField("Category", move.Category, true)
                        .AddField("Accuracy", move.Accuracy != null ? $"{move.Accuracy.Value}" : "-", true)
                        .AddField("Power", move.BasePower, true)
                        .AddField("PP", move.Pp, true)
                        .AddField("Priority", move.Priority, true))
                    .ConfigureAwait(false);
            }

            /*[RokiCommand, Usage, Description, Aliases]
            public async Task Nature([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;
                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                try
                {
                    var nature = await _pokeClient.GetResourceAsync<Nature>(query).ConfigureAwait(false);
                    
                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle(nature.Name.ToTitleCase());
                    
                    // checks if nature is not neutral
                    if (nature.IncreasedStat != null)    
                        embed.AddField("Increased Stat", nature.IncreasedStat.Name.ToTitleCase().Replace('-', ' '), true)
                        .AddField("Decreased Stat", nature.DecreasedStat.Name.ToTitleCase().Replace('-', ' '), true)
                        .AddField("Likes Flavor", nature.LikesFlavor.Name.ToTitleCase(), true)
                        .AddField("Hates Flavor", nature.HatesFlavor.Name.ToTitleCase(), true);
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
            }*/

            /*[RokiCommand, Usage, Description, Aliases]
            public async Task Item([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;
                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                try
                {
                    var item = await _pokeClient.GetResourceAsync<Item>(query).ConfigureAwait(false);

                    var embed = new EmbedBuilder().WithOkColor()
                        .WithAuthor(item.Name.ToTitleCase().Replace('-', ' '))
                        .WithDescription(item.EffectEntries[0].Effect)
                        .WithThumbnailUrl(item.Sprites.Default)
                        .AddField("Category", item.Category.Name.ToTitleCase().Replace('-', ' '), true)
                        .AddField("Cost", item.Cost, true);
                    if (item.FlingPower != null)
                        embed.AddField("Fling Power", item.FlingPower, true);
                    
                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync("Item not found.").ConfigureAwait(false);
                }
            }*/
        }
    }
}