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

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class JeopardyCommands : RokiSubmodule<JeopardyService>
        {
            private readonly ICurrencyService _currency;
            private readonly DiscordSocketClient _client;

            public JeopardyCommands(DiscordSocketClient client, ICurrencyService currency)
            {
                _client = client;
                _currency = currency;
            }

            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            [RokiOptions(typeof(JeopardyArgs))]
            public async Task Jeopardy(params string[] args)
            {
                var opts = OptionsParser.ParseFrom(new JeopardyArgs(), args);
                
                var channel = (ITextChannel) Context.Channel;
                var questions = Service.GenerateGame(opts.NumCategories);

                var jeopardy = new Jeopardy(_client, questions, channel.Guild, channel, _currency, Service.GetFinalJeopardy());

                if (Service.ActiveGames.TryAdd(channel.Id, jeopardy))
                {
                    try
                    {
                        await jeopardy.StartGame().ConfigureAwait(false);
                    }
                    finally
                    {
                        Service.ActiveGames.TryRemove(channel.Id, out jeopardy);
                        await jeopardy.EnsureStopped().ConfigureAwait(false);
                    }
                    return;
                }

                await Context.Channel.SendErrorAsync($"Jeopardy game is already in progress.").ConfigureAwait(false);
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithColor(jeopardy.Color)
                        .WithAuthor("Jeopardy!")
                        .WithTitle($"{jeopardy.CurrentClue.Category} - ${jeopardy.CurrentClue.Value}")
                        .WithDescription(jeopardy.CurrentClue.Clue))
                    .ConfigureAwait(false);
            }

            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task JeopardyLeaderboard()
            {
                if (Service.ActiveGames.TryGetValue(Context.Channel.Id, out var jeopardy))
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
            [RequireContext(ContextType.Guild)]
            public async Task JeopardyStop()
            {
                if (Service.ActiveGames.TryGetValue(Context.Channel.Id, out var jeopardy))
                {
                    await jeopardy.StopJeopardyGame().ConfigureAwait(false);
                    return;
                }
                
                await Context.Channel.SendErrorAsync("No active Jeopardy! game.").ConfigureAwait(false);
            }
            
            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task JeopardyVote()
            {
                if (Service.ActiveGames.TryGetValue(Context.Channel.Id, out var jeopardy))
                {
                    var code = jeopardy.VoteSkip(Context.User.Id);
                    if (code == 0)
                    {
                        await Task.Delay(250).ConfigureAwait(false);
                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithColor(jeopardy.Color)
                                .WithAuthor("Vote Skip")
                                .WithDescription(jeopardy.Votes.Count != jeopardy.Users.Count
                                    ? $"Voted\n`{jeopardy.Votes.Count}/{jeopardy.Users.Count}` required to skip."
                                    : $"Voted passed."))
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