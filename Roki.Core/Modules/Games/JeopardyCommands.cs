using System;
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

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class JeopardyCommands : RokiSubmodule<JeopardyService>
        {
            private readonly CurrencyService _currency;
            private readonly DiscordSocketClient _client;
            private readonly Roki _roki;

            public JeopardyCommands(DiscordSocketClient client, Roki roki)
            {
                _client = client;
                _roki = roki;
            }

            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task Jeopardy()
            {
                var channel = (ITextChannel) ctx.Channel;
                var questions = _service.GenerateGame();
                var jeopardy = new Jeopardy(_client, questions, channel.Guild, channel, _roki, _currency);

                if (_service.ActiveGames.TryAdd(channel.Id, jeopardy))
                {
                    try
                    {
                        await jeopardy.StartGame().ConfigureAwait(false);
                    }
                    finally
                    {
                        _service.ActiveGames.TryRemove(channel.Id, out jeopardy);
                        await jeopardy.EnsureStopped().ConfigureAwait(false);
                    }
                    return;
                }

                await ctx.Channel.SendErrorAsync($"Jeopardy game is already in progress.\n{jeopardy.CurrentClue}").ConfigureAwait(false);
            }

            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task JeopardyLeaderboard()
            {
                if (_service.ActiveGames.TryGetValue(ctx.Channel.Id, out var jeopardy))
                {
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithColor(Color.Blue)
                            .WithTitle("Jeopardy! Scores")
                            .WithDescription(jeopardy.GetLeaderboard()))
                        .ConfigureAwait(false);
                    return;
                }

                await ctx.Channel.SendErrorAsync("No active Jeopardy! game.").ConfigureAwait(false);
            }

            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task JeopardyStop()
            {
                if (_service.ActiveGames.TryGetValue(ctx.Channel.Id, out var jeopardy))
                {
                    await jeopardy.StopJeopardyGame().ConfigureAwait(false);
                    return;
                }
                
                await ctx.Channel.SendErrorAsync("No active Jeopardy! game.").ConfigureAwait(false);
            }
        }
    }
}