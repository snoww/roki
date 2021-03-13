using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Games.Common;
using Roki.Modules.Games.Services;
using Roki.Services;
using Roki.Services.Database.Models;

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        [RequireContext(ContextType.Guild)]
        public class JeopardyCommands : RokiSubmodule<JeopardyService>
        {
            private readonly DiscordSocketClient _client;
            private readonly ICurrencyService _currency;
            private readonly IConfigurationService _config;
            private readonly SemaphoreSlim _semaphore = new(1, 1);

            public JeopardyCommands(DiscordSocketClient client, ICurrencyService currency, IConfigurationService config)
            {
                _client = client;
                _currency = currency;
                _config = config;
            }

            [RokiCommand, Description, Aliases, Usage]
            [RokiOptions(typeof(JeopardyOptions))]
            public async Task Jeopardy(params string[] args)
            {
                JeopardyOptions opts = OptionsParser.ParseFrom(new JeopardyOptions(), args);

                if (Service.ActiveGames.TryGetValue(Context.Channel.Id, out Jeopardy activeGame))
                {
                    await Context.Channel.SendErrorAsync("Jeopardy game is already in progress in current channel.").ConfigureAwait(false);
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithColor(activeGame.Color)
                            .WithAuthor("Jeopardy!")
                            .WithTitle($"{activeGame.CurrentClue.Category.Name} - ${activeGame.CurrentClue.Value}")
                            .WithDescription(activeGame.CurrentClue.Text)
                            .WithFooter($"#{activeGame.CurrentClue.Id} | answer with a question, e.g. what's x, who's x"))
                        .ConfigureAwait(false);
                    return;
                }

                await _semaphore.WaitAsync();

                Jeopardy jeopardy;
                try
                {
                    List<Category> categories = await Service.GenerateGame(opts.NumCategories).ConfigureAwait(false);

                    jeopardy = new Jeopardy(_client, await _config.GetGuildConfigAsync(Context.Guild.Id), categories, Context.Guild, Context.Channel as ITextChannel, _currency, await Service.GenerateFinalJeopardy());
                }
                catch (Exception e)
                {
                    Log.Error(e, "Could not start jeopardy game");
                    _semaphore.Release();
                    return;
                }
                

                if (Service.ActiveGames.TryAdd(Context.Channel.Id, jeopardy))
                {
                    _semaphore.Release();
                    try
                    {
                        await jeopardy.StartGame().ConfigureAwait(false);
                    }
                    finally
                    {
                        Service.ActiveGames.TryRemove(Context.Channel.Id, out jeopardy);
                        if (jeopardy != null)
                        {
                            await jeopardy.EnsureStopped().ConfigureAwait(false);
                            jeopardy.Dispose();
                        }
                    }
                }
                else
                {
                    _semaphore.Release();
                    Log.Error("Could not start jeopardy game");
                    jeopardy.Dispose();
                }
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task JeopardyLeaderboard()
            {
                if (Service.ActiveGames.TryGetValue(Context.Channel.Id, out Jeopardy jeopardy))
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithColor(Color.Blue)
                            .WithTitle("Jeopardy! Scores")
                            .WithDescription(jeopardy.GetLeaderboard()))
                        .ConfigureAwait(false);
                    return;
                }

                await Context.Channel.SendErrorAsync("No active Jeopardy! game.").ConfigureAwait(false);
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task JeopardyStop()
            {
                if (Service.ActiveGames.TryGetValue(Context.Channel.Id, out Jeopardy jeopardy))
                {
                    await jeopardy.StopJeopardyGame().ConfigureAwait(false);
                    return;
                }

                await Context.Channel.SendErrorAsync("No active Jeopardy! game.").ConfigureAwait(false);
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task JeopardyVote()
            {
                if (Service.ActiveGames.TryGetValue(Context.Channel.Id, out Jeopardy jeopardy))
                {
                    int code = jeopardy.VoteSkip(Context.User.Id);
                    if (code == 0)
                    {
                        await Task.Delay(250).ConfigureAwait(false);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithColor(jeopardy.Color)
                                .WithAuthor("Vote Skip")
                                .WithDescription(jeopardy.Votes.Count != jeopardy.Users.Count
                                    ? $"Voted\n`{jeopardy.Votes.Count}/{jeopardy.Users.Count}` required to skip."
                                    : "Voted passed."))
                            .ConfigureAwait(false);
                    }
                    else if (code == -1)
                    {
                        await Context.Channel.SendErrorAsync("Cannot vote skip yet.").ConfigureAwait(false);
                    }
                    else if (code == -2)
                    {
                        await Context.Channel.SendErrorAsync("You need a score to vote skip.").ConfigureAwait(false);
                    }
                    else if (code == -3)
                    {
                        await Context.Channel.SendErrorAsync("You already voted.").ConfigureAwait(false);
                    }

                    return;
                }

                await Context.Channel.SendErrorAsync("No active Jeopardy! game.").ConfigureAwait(false);
            }
        }
    }
}