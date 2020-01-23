using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Games.Common;
using Roki.Modules.Games.Services;
using Roki.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class ShowdownCommands : RokiSubmodule<ShowdownService>
        {
            private readonly ICurrencyService _currency;
            private readonly DbService _db;
            private readonly DiscordSocketClient _client;

            public ShowdownCommands(ICurrencyService currency, DbService db, DiscordSocketClient client)
            {
                _currency = currency;
                _db = db;
                _client = client;
            }

            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task BetPokemonGame(int gen = 8)
            {
                if (gen > 8 || gen < 4) gen = 8;
                
                var showdown = new Showdown(_currency, _db, _client, (ITextChannel)ctx.Channel, gen);
                
                if (_service.ActiveGames.TryAdd(ctx.Channel.Id, showdown))
                {
                    await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                    try
                    {
                        await showdown.StartGameAsync().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _log.Warn(e, $"Error during '{showdown.GameId}'\n{e}");
                        await ctx.Channel.SendErrorAsync("Something went wrong with the current game. Please try again later.").ConfigureAwait(false);
                    }
                    finally
                    {
                        _service.ActiveGames.TryRemove(ctx.Channel.Id, out _);
                    }
                    
                    return;
                }

                await ctx.Channel.SendErrorAsync("Game already in progress in current channel.");
            }

            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task BetPokemonReplay([Leftover] string uid = null)
            {
                uid = uid.SanitizeStringFull();
                if (uid.Length != 8)
                {
                    await ctx.Channel.SendErrorAsync("Invalid Game ID").ConfigureAwait(false);
                    return;
                }

                _service.ActiveGames.TryGetValue(ctx.Channel.Id, out var showdown);
                if (showdown != null && showdown.GameId.Equals(uid, StringComparison.OrdinalIgnoreCase))
                {
                    await ctx.Channel.SendErrorAsync("Please wait for current game to finish first.").ConfigureAwait(false);
                    return;
                }

                var url = await ShowdownService.GetBetPokemonGame(uid);
                if (url == null)
                {
                    await ctx.Channel.SendErrorAsync("Cannot find replay with that ID.").ConfigureAwait(false);
                    return;
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription($"{ctx.User.Mention} Here is the replay url:\nhttps://replay.pokemonshowdown.com/{url}"))
                    .ConfigureAwait(false);
            }
        }
    }
}