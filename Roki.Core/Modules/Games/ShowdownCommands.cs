using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Games.Common;
using Roki.Modules.Games.Services;
using Roki.Services;

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        [RequireContext(ContextType.Guild)]
        public class ShowdownCommands : RokiSubmodule<ShowdownService>
        {
            private readonly ICurrencyService _currency;
            private readonly DiscordSocketClient _client;
            private readonly IRedisCache _cache;
            private readonly IConfigurationService _config;

            public ShowdownCommands(ICurrencyService currency, DiscordSocketClient client, IRedisCache cache, IConfigurationService config)
            {
                _currency = currency;
                _client = client;
                _cache = cache;
                _config = config;
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task BetPokemonGame(int gen = 8)
            {
                if (gen > 8 || gen < 4) gen = 8;
                
                
                if (Service.ActiveGames.ContainsKey(Context.Channel.Id))
                {
                    await Context.Channel.SendErrorAsync("Game already in progress in current channel.");
                    return;
                }
                
                var showdown = new Showdown(_currency, _client, await _config.GetGuildConfigAsync(Context.Guild.Id), (ITextChannel)Context.Channel, gen, _cache);

                if (Service.ActiveGames.TryAdd(Context.Channel.Id, showdown))
                {
                    await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
                    try
                    {
                        await showdown.StartGameAsync().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Log.Warn(e, $"Error during '{showdown.GameId}'\n{e}");
                        await Context.Channel.SendErrorAsync("Something went wrong with the current game. Please try again later.").ConfigureAwait(false);
                    }
                    finally
                    {
                        Service.ActiveGames.TryRemove(Context.Channel.Id, out _);
                    }
                }
            }

            // unavailable since running on custom showdown server
            // todo parse html replay
            /*[RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task BetPokemonReplay([Leftover] string uid = null)
            {
                uid = uid.SanitizeStringFull();
                if (uid.Length != 8)
                {
                    await Context.Channel.SendErrorAsync("Invalid Game ID").ConfigureAwait(false);
                    return;
                }

                Service.ActiveGames.TryGetValue(Context.Channel.Id, out var showdown);
                if (showdown != null && showdown.GameId.Equals(uid, StringComparison.OrdinalIgnoreCase))
                {
                    await Context.Channel.SendErrorAsync("Please wait for current game to finish first.").ConfigureAwait(false);
                    return;
                }
                
                var url = await Service.GetBetPokemonGame(uid);
                if (url == null)
                {
                    await Context.Channel.SendErrorAsync("Cannot find replay with that ID.").ConfigureAwait(false);
                    return;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context).WithDescription($"{Context.User.Mention} Here is the replay url:\nhttps://replay.pokemonshowdown.com/{url}"))
                    .ConfigureAwait(false);
            }*/
        }
    }
}