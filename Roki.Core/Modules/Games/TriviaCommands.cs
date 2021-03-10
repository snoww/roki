using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
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
        public class TriviaCommands : RokiSubmodule<TriviaService>
        {
            private static readonly IEmote LetterA = new Emoji("🇦");
            private static readonly IEmote LetterB = new Emoji("🇧");
            private static readonly IEmote LetterC = new Emoji("🇨");
            private static readonly IEmote LetterD = new Emoji("🇩");
            private static readonly IEmote Check = new Emoji("✔");
            private static readonly IEmote Cross = new Emoji("❌");

            private static readonly IEmote[] MultipleChoice =
            {
                LetterA,
                LetterB,
                LetterC,
                LetterD
            };

            private static readonly IEmote[] TrueFalse =
            {
                Check,
                Cross
            };

            private static readonly Dictionary<string, int> Categories = new()
            {
                {"All", 0},
                {"General Knowledge", 9},
                {"Books", 10},
                {"Film", 11},
                {"Music", 12},
//                {"Musicals & Theatres", 13},
                {"Musicals", 13},
                {"Theatre", 13},
                {"Television", 14},
                {"Video Games", 15},
                {"Board Games", 16},
//                {"Science & Nature", 17},
                {"Science", 17},
                {"Nature", 17},
                {"Computers", 18},
                {"Mathematics", 19},
                {"Mythology", 20},
                {"Sports", 21},
                {"Geography", 22},
                {"History", 23},
                {"Politics", 24},
                {"Art", 25},
                {"Celebrities", 26},
                {"Animals", 27},
                {"Vehicles", 28},
                {"Comics", 29},
                {"Gadgets", 30},
//                {"Japanese Anime & Manga", 31},
                {"Anime", 31},
                {"Manga", 31},
//                {"Cartoons & Animations", 32},
                {"Cartoons", 32},
                {"Animations", 32}
            };

            private readonly DiscordSocketClient _client;
            private readonly IConfigurationService _config;
            private readonly ICurrencyService _currency;

            public TriviaCommands(ICurrencyService currency, DiscordSocketClient client, IConfigurationService config)
            {
                _currency = currency;
                _client = client;
                _config = config;
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Trivia([Leftover] string category = "All")
            {
                if (Service.TriviaGames.ContainsKey(Context.Channel.Id))
                {
                    await Context.Channel.SendErrorAsync("Game already in progress in current channel.");
                    return;
                }

                category = category.Trim().ToTitleCase();
                if (!Categories.ContainsKey(category))
                {
                    await Context.Channel.SendErrorAsync("Unknown Category, use `.tc` to checkout the trivia categories.").ConfigureAwait(false);
                    return;
                }
                
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

                Service.TriviaGames.TryAdd(Context.Channel.Id, Context.User.Id);
                TriviaModel questions;
                var prizePool = 0;
                try
                {
                    await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
                    questions = await Service.GetTriviaQuestionsAsync(Categories[category]).ConfigureAwait(false);
                    foreach (Results question in questions.Results)
                    {
                        if (question.Difficulty == "easy")
                        {
                            prizePool += guildConfig.TriviaEasy;
                        }
                        else if (question.Difficulty == "medium")
                        {
                            prizePool += guildConfig.TriviaMedium;
                        }
                        else
                        {
                            prizePool += guildConfig.TriviaHard;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Warn(e);
                    await Context.Channel.SendErrorAsync("Unable to start game, please try again.");
                    Service.TriviaGames.TryRemove(Context.Channel.Id, out _);
                    return;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle($"Trivia Game - {category}")
                        .WithDescription(
                            $"Starting new trivia game.\nYou can earn up to `{prizePool:N0}` {guildConfig.CurrencyNamePlural}!\nReact with the correct emote to answer questions.\nType `stop` to cancel game early"))
                    .ConfigureAwait(false);

                await Task.Delay(5000).ConfigureAwait(false);

                var exit = false;
                var toDelete = new List<IUserMessage>();
                _client.MessageReceived += StopReceived;

                Task StopReceived(SocketMessage message)
                {
                    if (message.Channel.Id != Context.Channel.Id || message.Author.IsBot || !message.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.CompletedTask;
                    }

                    exit = true;
                    return Task.CompletedTask;
                }

                var count = 1;
                var playerScore = new Dictionary<IUser, PlayerScore>();
                foreach (Results q in questions.Results)
                {
                    var playerChoice = new Dictionary<IUser, string>();
                    List<string> shuffledAnswers = Service.RandomizeAnswersOrder(q.Correct, q.Incorrect);
                    string question = HttpUtility.HtmlDecode(q.Question);
                    string answer = shuffledAnswers.First(s => s.Contains(HttpUtility.HtmlDecode(q.Correct) ?? ""));
                    int difficultyBonus;
                    if (q.Difficulty == "easy")
                    {
                        difficultyBonus = guildConfig.TriviaEasy;
                    }
                    else if (q.Difficulty == "medium")
                    {
                        difficultyBonus = guildConfig.TriviaMedium;
                    }
                    else
                    {
                        difficultyBonus = guildConfig.TriviaHard;
                    }

                    IUserMessage msg;

                    EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle($"Question {count++}: {q.Category.ToTitleCase()} - {q.Difficulty.ToTitleCase()}")
                        .WithFooter("Type stop to stop trivia game.");
                    if (q.Type == "multiple")
                    {
                        embed.WithDescription($"**Multiple Choice**\n{question}\n{string.Join('\n', shuffledAnswers)}");
                        msg = await Context.Channel.EmbedAsync(embed);
                        await msg.AddReactionsAsync(MultipleChoice).ConfigureAwait(false);
                    }
                    else
                    {
                        embed.WithDescription($"**True or False**\n{question}");
                        msg = await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                        await msg.AddReactionsAsync(TrueFalse).ConfigureAwait(false);
                    }

                    toDelete.Add(msg);
                    var answers = false;
                    using (msg.OnReaction(_client, AnswerAdded))
                    {
                        await Task.Delay(20000).ConfigureAwait(false);
                    }

                    if (exit || !answers)
                    {
                        _client.MessageReceived -= StopReceived;
                        if (!answers)
                        {
                            await Context.Channel.SendErrorAsync("Trivia stopped due to inactivity.").ConfigureAwait(false);
                        }
                        else
                        {
                            await Context.Channel.SendErrorAsync("Current trivia game stopped.").ConfigureAwait(false);
                        }

                        Service.TriviaGames.TryRemove(Context.Channel.Id, out _);
                        await ((ITextChannel) Context.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
                        return;
                    }

                    var corrStr = "";
                    var incorrStr = "";

                    foreach ((IUser user, string score) in playerChoice)
                    {
                        if (score != answer)
                        {
                            if (playerScore.ContainsKey(user))
                            {
                                playerScore[user].Incorrect += 1;
                            }
                            else
                            {
                                playerScore.Add(user, new PlayerScore
                                {
                                    Incorrect = 1
                                });
                            }

                            incorrStr += user.Username + '\n';
                            continue;
                        }

                        if (playerScore.ContainsKey(user))
                        {
                            playerScore[user].Amount += difficultyBonus;
                            playerScore[user].Correct += 1;
                        }
                        else
                        {
                            playerScore.Add(user, new PlayerScore
                            {
                                Amount = difficultyBonus,
                                Correct = 1
                            });
                        }

                        corrStr += user.Username + '\n';
                    }

                    IUserMessage ans = await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithTitle("Answer")
                            .WithDescription($"{answer}")
                            .AddField("Correct", !string.IsNullOrWhiteSpace(corrStr) ? corrStr : "None", true)
                            .AddField("Incorrect", !string.IsNullOrWhiteSpace(incorrStr) ? incorrStr : "None", true))
                        .ConfigureAwait(false);
                    toDelete.Add(ans);
                    await Task.Delay(5000).ConfigureAwait(false);

                    async Task AnswerAdded(SocketReaction r)
                    {
                        List<IEmote> mc = MultipleChoice.ToList();
                        if (r.Channel != Context.Channel || r.User.Value.IsBot || r.Message.Value.Id != msg.Id)
                        {
                            return;
                        }

                        if (mc.Contains(r.Emote))
                        {
                            if (playerChoice.ContainsKey(r.User.Value))
                            {
                                playerChoice[r.User.Value] = shuffledAnswers[mc.IndexOf(r.Emote)];
                            }
                            else
                            {
                                playerChoice.Add(r.User.Value, shuffledAnswers[mc.IndexOf(r.Emote)]);
                            }
                        }
                        else if (TrueFalse.Contains(r.Emote))
                        {
                            if (playerChoice.ContainsKey(r.User.Value))
                            {
                                playerChoice[r.User.Value] = r.Emote.Equals(Check) ? "True" : "False";
                            }
                            else
                            {
                                playerChoice.Add(r.User.Value, r.Emote.Equals(Check) ? "True" : "False");
                            }
                        }

                        answers = true;
                        await Task.CompletedTask;
                        await Task.Delay(500);
                        await msg.RemoveReactionAsync(r.Emote, r.User.Value).ConfigureAwait(false);
                    }
                }

                var winStr = "";
                var scoreStr = "";
                var winners = false;
                foreach ((IUser user, PlayerScore score) in playerScore)
                {
                    scoreStr += $"{user.Username} `{score.Correct}`/`{score.Incorrect + score.Correct}`\n";
                    long before = await _currency.GetCurrencyAsync(Context.User.Id, Context.Guild.Id);
                    await _currency.AddCurrencyAsync(user.Id, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, "Trivia Reward", score.Amount)
                        .ConfigureAwait(false);
                    winStr += $"{user.Username} won `{score.Amount:N0}` {guildConfig.CurrencyIcon}\n" +
                              $"\t`{before:N0}` ⇒ `{await _currency.GetCurrencyAsync(Context.User.Id, Context.Guild.Id):N0:N0}`\n";
                    winners = true;
                }

                if (winners)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithDescription($"Congratulations!\n{winStr}\n{scoreStr}")).ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendErrorAsync($"Better luck next time!\n{scoreStr}").ConfigureAwait(false);
                }

                Service.TriviaGames.TryRemove(Context.Channel.Id, out _);
                await Task.Delay(5000).ConfigureAwait(false);
                await ((ITextChannel) Context.Channel).DeleteMessagesAsync(toDelete).ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task TriviaCategories()
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle("Trivia Categories")
                        .WithDescription(string.Join(", ", Categories.Keys)))
                    .ConfigureAwait(false);
            }

            private class PlayerScore
            {
                public int Correct { get; set; }
                public int Incorrect { get; set; }
                public long Amount { get; set; }
            }
        }
    }
}