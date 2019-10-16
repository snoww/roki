using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore.Internal;
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
        public class TriviaCommands : RokiSubmodule<TriviaService>
        {
            private readonly ICurrencyService _currency;
            private readonly DiscordSocketClient _client;
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
            
            private static readonly Dictionary<string, int> Categories = new Dictionary<string, int>
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

            public class PlayerScore
            {
                public int Correct { get; set; } = 0;
                public int Incorrect { get; set; } = 0;
                public long Amount { get; set; } = 0;
            }

            public TriviaCommands(ICurrencyService currency, DiscordSocketClient client)
            {
                _currency = currency;
                _client = client;
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Trivia(string category = "All")
            {
                if (_service.TriviaGames.ContainsKey(ctx.Channel.Id))
                {
                    await ctx.Channel.SendErrorAsync("Game already in progress in current channel.");
                    return;
                }

                category = category.Trim().ToTitleCase();
                if (!Categories.ContainsKey(category))
                {
                    await ctx.Channel.SendErrorAsync("Unknown Category, use `.tc` to checkout the trivia categories.").ConfigureAwait(false);
                    return;
                }

                _service.TriviaGames.TryAdd(ctx.Channel.Id, ctx.User.Id);
                TriviaModel questions;
                var prizePool = 0;
                try
                {
                    await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                    questions = await _service.GetTriviaQuestionsAsync(Categories[category]).ConfigureAwait(false);
                    foreach (var question in questions.Results)
                    {
                        if (question.Difficulty == "easy")
                            prizePool += 1;
                        else if (question.Difficulty == "medium")
                            prizePool += 3;
                        else
                            prizePool += 5;
                    }
                }
                catch (Exception e)
                {
                    _log.Warn(e);
                    await ctx.Channel.SendErrorAsync("Unable to start game, please try again.");
                    _service.TriviaGames.TryRemove(ctx.Channel.Id, out _);
                    return;
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle($"Trivia Game - {category}")
                        .WithDescription($"Starting new trivia game.\nYou can earn up to **{prizePool}** Stones!\nReact with the correct emote to answer questions."))
                    .ConfigureAwait(false);

                await Task.Delay(5000).ConfigureAwait(false);
                
                var count = 1;
                var playerScore = new Dictionary<IUser, PlayerScore>();
                foreach (var q in questions.Results)
                {
                    var playerChoice = new Dictionary<IUser, string>();
                    var shuffledAnswers = _service.RandomizeAnswersOrder(q.Correct, q.Incorrect);
                    var question = HttpUtility.HtmlDecode(q.Question);
                    var answer = shuffledAnswers.First(s => s.Contains(HttpUtility.HtmlDecode(q.Correct) ?? ""));
                    int difficultyBonus;
                    if (q.Difficulty == "easy")
                        difficultyBonus = 1;
                    else if (q.Difficulty == "medium")
                        difficultyBonus = 3;
                    else
                        difficultyBonus = 5;
                    IUserMessage msg;

                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle($"Question {count++}: {q.Category.ToTitleCase()} - {q.Difficulty.ToTitleCase()}");
                    if (q.Type == "multiple")
                    {
                        embed.WithDescription($"**Multiple Choice**\n{question}\n{string.Join('\n', shuffledAnswers)}");
                        msg = await ctx.Channel.EmbedAsync(embed);
                        await msg.AddReactionsAsync(MultipleChoice).ConfigureAwait(false);
                    }
                    else
                    {
                        embed.WithDescription($"**True or False**\n{question}");
                        msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                        await msg.AddReactionsAsync(TrueFalse).ConfigureAwait(false);
                    }

                    using (msg.OnReaction(_client, AnswerAdded))
                    {
                        await Task.Delay(20000).ConfigureAwait(false);
                    }

                    var corrStr = "";
                    var incorrStr = "";
                    
                    foreach (var (user, score) in playerChoice)
                    {
                        if (score != answer)
                        {
                            if (playerScore.ContainsKey(user))
                                playerScore[user].Incorrect += 1;
                            else
                                playerScore.Add(user, new PlayerScore
                                {
                                    Incorrect = 1
                                });
                            incorrStr += user.Username + '\n';
                            continue;
                        }

                        if (playerScore.ContainsKey(user))
                        {
                            playerScore[user].Amount += difficultyBonus;
                            playerScore[user].Correct += 1;
                        }
                        else
                            playerScore.Add(user, new PlayerScore
                            {
                                Amount = difficultyBonus,
                                Correct = 1
                            });
                        corrStr += user.Username + '\n';
                    }
                    
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("Answer")
                            .WithDescription($"{answer}")
                            .AddField("Correct", !string.IsNullOrWhiteSpace(corrStr) ? corrStr : "None", true)
                            .AddField("Incorrect", !string.IsNullOrWhiteSpace(incorrStr) ? incorrStr : "None", true))
                        .ConfigureAwait(false);
                    
                    await Task.Delay(5000).ConfigureAwait(false);

                    async Task AnswerAdded(SocketReaction r)
                    {
                        if (r.Channel != ctx.Channel || r.User.Value.IsBot || r.Message.Value.Id != msg.Id)
                            return;
                        if (MultipleChoice.Contains(r.Emote))
                        {
                            if (playerChoice.ContainsKey(r.User.Value))
                                playerChoice[r.User.Value] = shuffledAnswers[MultipleChoice.IndexOf(r.Emote)];
                            else
                                playerChoice.Add(r.User.Value, shuffledAnswers[MultipleChoice.IndexOf(r.Emote)]);
                        }
                        else if (TrueFalse.Contains(r.Emote))
                        {
                            if (playerChoice.ContainsKey(r.User.Value))
                                playerChoice[r.User.Value] = r.Emote.Equals(Check) ? "True" : "False";
                            else
                                playerChoice.Add(r.User.Value, r.Emote.Equals(Check) ? "True" : "False");
                        }

                        await Task.CompletedTask;
                        await Task.Delay(500);
                        await msg.RemoveReactionAsync(r.Emote, r.User.Value).ConfigureAwait(false);
                    }

                    /*async Task AnswerRemoved(SocketReaction r)
                    {
                        if (r.Channel != ctx.Channel || r.User.Value.IsBot || r.Message.Value != msg)
                            await Task.CompletedTask;
                        if (MultipleChoice.Contains(r.Emote))
                        {
                            if (playerChoice.ContainsKey(r.User.Value))
                                playerChoice.Remove(r.User.Value, out _);
                            await Task.CompletedTask;
                        }
                        if (TrueFalse.Contains(r.Emote))
                        {
                            if (playerChoice.ContainsKey(r.User.Value))
                                playerChoice.Remove(r.User.Value, out _);
                            await Task.CompletedTask;
                        }
                    }*/
                }

                var winStr = "";
                var scoreStr = "";
                var winners = false;
                foreach (var (user, score) in playerScore)
                {
                    scoreStr += $"{user.Username} {score.Correct}/{score.Incorrect + score.Correct}\n";
                    if (score.Amount <= 0 || score.Correct / (float) (score.Correct + score.Incorrect) < 0.6) continue;
                    winStr += $"{user.Username} won {score.Amount} stones\n";
                    winners = true;
                    await _currency.ChangeAsync(user, "Trivia Reward", score.Amount, "Server", user.Id.ToString(), ctx.Guild.Id, ctx.Channel.Id,
                        ctx.Message.Id).ConfigureAwait(false);
                }

                if (winners)
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithDescription($"Congratulations!\n{winStr}\n{scoreStr}")).ConfigureAwait(false);
                else
                    await ctx.Channel.SendErrorAsync($"Better luck next time!\n{scoreStr}").ConfigureAwait(false);
                
                _service.TriviaGames.TryRemove(ctx.Channel.Id, out _);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task TriviaCategories()
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("Trivia Categories")
                        .WithDescription(string.Join(", ", Categories.Keys)))
                    .ConfigureAwait(false);
            }
        }
    }
}