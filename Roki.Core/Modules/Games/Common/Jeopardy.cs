using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NLog;
using Roki.Common;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Models;

namespace Roki.Modules.Games.Common
{
    public class Jeopardy : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ITextChannel _channel;
        private readonly ICurrencyService _currency;
        private readonly DiscordSocketClient _client;
        private readonly GuildConfig _config;
        private readonly List<Category> _categories;
        private readonly ConcurrentBag<bool> _confirmed = new();
        private readonly Category _finalJeopardy;
        private readonly Dictionary<ulong, string> _finalJeopardyAnswers = new();
        private readonly SemaphoreSlim _guess = new(1, 1);
        private readonly SemaphoreSlim _voteSkip = new(1, 1);
        private readonly JaroWinkler _jw = new();

        private const double MinSimilarity = 0.95;

        private readonly IGuild _guild;

        public readonly Color Color = Color.DarkBlue;

        public readonly ConcurrentDictionary<IUser, int> Users = new();

        private CancellationTokenSource _cancel;

        private bool _canGuess;
        private bool _canVote;
        private int _guessCount;
        private bool _stopGame;

        public Jeopardy(DiscordSocketClient client, GuildConfig config, List<Category> categories, IGuild guild, ITextChannel channel, ICurrencyService currency, Category finalJeopardy)
        {
            _client = client;
            _config = config;
            _categories = categories;
            _guild = guild;
            _channel = channel;
            _currency = currency;
            _finalJeopardy = finalJeopardy;
        }

        public Clue CurrentClue { get; private set; }

        public HashSet<ulong> Votes { get; } = new();

        public async Task StartGame()
        {
            await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithTitle("Jeopardy!")
                    .WithDescription("Welcome to Jeopardy!\nGame is starting soon...")
                    .WithFooter("Responses must be in question form"))
                .ConfigureAwait(false);
            await _channel.TriggerTypingAsync().ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            while (!_stopGame)
            {
                _cancel = new CancellationTokenSource();

                await ShowCategories().ConfigureAwait(false);
                SocketMessage catResponse = await ReplyHandler(_channel.Id, timeout: TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                CategoryStatus catStatus = ParseCategoryAndClue(catResponse);
                while (catStatus != CategoryStatus.Success)
                {
                    if (catStatus == CategoryStatus.UnavailableClue)
                    {
                        await _channel.SendErrorAsync("That clue is not available.\nPlease select another clue.").ConfigureAwait(false);
                    }
                    else if (catStatus == CategoryStatus.WrongAmount)
                    {
                        await _channel.SendErrorAsync("There are no clues available for that amount.\nPlease try again.")
                            .ConfigureAwait(false);
                    }
                    else if (catStatus == CategoryStatus.WrongCategory)
                    {
                        await _channel.SendErrorAsync("Invalid category.\nPlease try again.").ConfigureAwait(false);
                    }
                    else if (catStatus == CategoryStatus.FalsePositiveInput)
                    {
                        //
                    }
                    else
                    {
                        await _channel.SendErrorAsync("No response received, stopping Jeopardy!").ConfigureAwait(false);
                        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        return;
                    }

                    catResponse = await ReplyHandler(_channel.Id, timeout: TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                    catStatus = ParseCategoryAndClue(catResponse);
                }

                // CurrentClue is now the chosen clue
                await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                        .WithAuthor("Jeopardy!")
                        .WithTitle($"{CurrentClue.Category.Name.EscapeMarkdown()} - ${CurrentClue.Value}")
                        .WithDescription(CurrentClue.Text.EscapeMarkdown())
                        .WithFooter($"#{CurrentClue.Id} | answer with a question, e.g. what's x, who's x"))
                    .ConfigureAwait(false);

                try
                {
                    _client.MessageReceived += GuessHandler;
                    _canGuess = true;
                    await VoteDelay().ConfigureAwait(false);
                    try
                    {
                        // todo config
                        await Task.Delay(TimeSpan.FromSeconds(45), _cancel.Token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        // correct answer
                    }
                }
                finally
                {
                    _canGuess = false;
                    _canVote = false;
                    _guessCount = 0;
                    _client.MessageReceived -= GuessHandler;
                }

                if (!_cancel.IsCancellationRequested)
                {
                    await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color.Red)
                            .WithAuthor("Jeopardy!")
                            .WithTitle("Times Up!")
                            .WithDescription($"The correct answer was:\n`{CurrentClue.Answer}`"))
                        .ConfigureAwait(false);
                }

                if (!_categories.SelectMany(category => category.Clues).Any(clue => clue.Available)) 
                    break;
                await Task.Delay(TimeSpan.FromSeconds(7)).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            if (!_stopGame && !Users.IsEmpty)
            {
                await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                        .WithAuthor("Jeopardy!")
                        .WithTitle("Current Winnings")
                        .WithDescription(GetLeaderboard()))
                    .ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await StartFinalJeopardy().ConfigureAwait(false);
            }
        }

        public async Task EnsureStopped()
        {
            _stopGame = true;
            IUserMessage msg = await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Jeopardy!")
                    .WithTitle("Final Winnings")
                    .WithDescription(GetLeaderboard()))
                .ConfigureAwait(false);

            if (!Users.Any()) return;
            // todo maybe require at least x many questions answered correctly in order to get winnings
            foreach ((IUser user, int winnings) in Users)
            {
                await _currency.AddCurrencyAsync(user.Id, _guild.Id, _channel.Id, msg.Id,"Jeopardy Winnings", winnings)
                    .ConfigureAwait(false);
            }
        }

        public async Task StopJeopardyGame()
        {
            bool old = _stopGame;
            _stopGame = true;
            if (!old)
            {
                await _channel.SendErrorAsync("Jeopardy! game stopping after this question.").ConfigureAwait(false);
            }
        }

        private async Task ShowCategories()
        {
            EmbedBuilder embed = new EmbedBuilder().WithColor(Color)
                .WithTitle("Jeopardy!")
                .WithDescription($"Please choose an available category and price from below.\ne.g. `{_categories.First().Name}` for `200`");
            foreach (Category category in _categories)
            {
                embed.AddField($"{category.Name.EscapeMarkdown()}", string.Join("\n", category.Clues.Select(c => $"{(c.Available ? $"`${c.Value}`" : $"~~`${c.Value}`~~")}")), true);
            }

            await _channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private CategoryStatus ParseCategoryAndClue(IMessage msg)
        {
            if (msg == null) return CategoryStatus.NoResponse;

            string message = msg.Content.ToLowerInvariant();
            if (message.Contains(" for ", StringComparison.Ordinal))
            {
                string userCategory = message[..message.LastIndexOf(" for ", StringComparison.Ordinal)].SanitizeStringFull();
                string price = message[message.LastIndexOf(" for ", StringComparison.Ordinal)..];
                int amount = int.Parse(new string(price.Where(char.IsDigit).ToArray()));

                Category category = _categories.FirstOrDefault(c => c.Name.SanitizeStringFull().ToLowerInvariant().Contains(userCategory, StringComparison.Ordinal));
                
                if (category == null)
                {
                    foreach (Category c in _categories.Where(c => c.Name.Split().Any(cName => _jw.Similarity(cName.SanitizeStringFull().ToLowerInvariant(), message) >= MinSimilarity)))
                    {
                        category = c;
                        break;
                    }
                }
                
                if (category == null)
                {
                    return CategoryStatus.WrongCategory;
                }

                Clue clue = category.Clues.FirstOrDefault(c => c.Value == amount);
                if (clue == null) return CategoryStatus.WrongAmount;
                if (!clue.Available) return CategoryStatus.UnavailableClue;
                CurrentClue = clue;
                CurrentClue.Available = false;
                return CategoryStatus.Success;
            }
            else
            {
                // this section is to parse category choice if they didn't include 'for' in reply
                string[] userCategory = message.Split();
                Category category = null;
                foreach (string userCat in userCategory[..^1])
                {
                    foreach (Category cat in _categories.Where(cat => cat.Name.SanitizeStringFull().Contains(userCat.SanitizeStringFull(), StringComparison.OrdinalIgnoreCase)))
                    {
                        category = cat;
                        goto found;
                    }
                    
                    foreach (Category cat in _categories.Where(cat => cat.Name.ToLowerInvariant().Split().Any(cName => _jw.Similarity(cName.SanitizeStringFull(), userCat) >= MinSimilarity)))
                    {
                        category = cat;
                        goto found;
                    }
                }
                
                found:

                if (category == null)
                {
                    return CategoryStatus.FalsePositiveInput;
                }

                if (!int.TryParse(userCategory[^1], out int amount))
                {
                    return CategoryStatus.WrongAmount;
                }
                
                Clue clue = category.Clues.FirstOrDefault(c => c.Value == amount);
                if (clue == null) return CategoryStatus.WrongAmount;
                if (!clue.Available) return CategoryStatus.UnavailableClue;
                CurrentClue = clue;
                CurrentClue.Available = false;
                return CategoryStatus.Success;
            }
        }

        private Task GuessHandler(SocketMessage msg)
        {
            Task _ = Task.Run(async () =>
            {
                try
                {
                    if (msg.Channel.Id != _channel.Id) return;
                    if (_canVote && !Users.IsEmpty && Votes.Count >= Users.Count)
                    {
                        Votes.Clear();
                        _cancel.Cancel();
                        // todo maybe reaction to skip vote??
                        await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                                .WithAuthor("Jeopardy!")
                                .WithTitle($"{CurrentClue.Category.Name.EscapeMarkdown()} - ${CurrentClue.Value}")
                                .WithDescription($"Vote skip passed.\nThe correct answer was:\n`{CurrentClue.Answer}`"))
                            .ConfigureAwait(false);
                        return;
                    }

                    if (msg.Author.IsBot || !Regex.IsMatch(msg.Content.ToLowerInvariant(), "^what|where|who")) return;
                    var guess = false;
                    await _guess.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        if (_canGuess && CurrentClue.CheckAnswer(msg.Content) && !_cancel.IsCancellationRequested)
                        {
                            Users.AddOrUpdate(msg.Author, CurrentClue.Value, (u, old) => old + CurrentClue.Value);
                            guess = true;
                        }
                    }
                    finally
                    {
                        _guess.Release();
                    }

                    if (!guess)
                    {
                        if (++_guessCount > 6)
                        {
                            _guessCount = 0;
                            await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                                    .WithAuthor("Jeopardy!")
                                    .WithTitle($"{CurrentClue.Category.Name.EscapeMarkdown()} - ${CurrentClue.Value}")
                                    .WithDescription(CurrentClue.Text.EscapeMarkdown())
                                    .WithFooter($"#{CurrentClue.Id} | answer with a question, e.g. what's x, who's x"))
                                .ConfigureAwait(false);
                        }

                        return;
                    }

                    _cancel.Cancel();
                    await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                            .WithAuthor("Jeopardy!")
                            .WithTitle($"{CurrentClue.Category.Name.EscapeMarkdown()} - ${CurrentClue.Value}")
                            .WithDescription($"{msg.Author.Mention} Correct.\nThe correct answer was:\n`{CurrentClue.Answer}`\n" +
                                             $"Your total score is: `{Users[msg.Author]:N0}`"))
                        .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Warn(e);
                }
            });
            return Task.CompletedTask;
        }

        private async Task StartFinalJeopardy()
        {
            await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Jeopardy!")
                    .WithTitle("Final Jeopardy!")
                    .WithDescription("Please go to your DMs to play the Final Jeopardy!")
                    .WithFooter("You must have a score to participate."))
                .ConfigureAwait(false);

            foreach ((IUser user, int amount) in Users)
            {
                _confirmed.Add(true);
                await DmFinalJeopardy(user, amount).ConfigureAwait(false);
            }

            while (!_confirmed.IsEmpty) await Task.Delay(1000).ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Final Jeopardy!")
                    .WithTitle($"{_finalJeopardy.Name}")
                    .WithDescription(_finalJeopardy.Clues.Single().Text.EscapeMarkdown()+ "\n\nContestants have 60 seconds to answer the question.")
                    .WithFooter($"#{_finalJeopardy.Id} | answers will NOT be checked here"))
                .ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromSeconds(60)).ConfigureAwait(false);

            await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Final Jeopardy!")
                    .WithDescription($"The correct answer is:\n`{_finalJeopardy.Clues.Single().Answer}`"))
                .ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);

            await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Final Jeopardy!")
                    .WithTitle("Results")
                    .WithDescription(_finalJeopardyAnswers.Count > 0 ? string.Join("\n", _finalJeopardyAnswers.Values).TrimTo(2048) : "No wagers."))
                .ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }

        private Task DmFinalJeopardy(IUser user, int amount)
        {
            Task _ = Task.Run(async () =>
            {
                try
                {
                    IDMChannel dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                    await dm.EmbedAsync(new EmbedBuilder().WithColor(Color)
                            .WithAuthor("Final Jeopardy!")
                            .WithTitle(_finalJeopardy.Name)
                            .WithDescription($"Please make your wager. You're current score is: `${amount:N0}`"))
                        .ConfigureAwait(false);

                    SocketMessage response = await ReplyHandler(dm.Id, true).ConfigureAwait(false);
                    int wager = -1;
                    while (wager == -1)
                    {
                        if (response == null)
                        {
                            await dm.SendErrorAsync("No response received. You are removed from the Final Jeopardy!").ConfigureAwait(false);
                            _confirmed.TryTake(out bool _);
                            return;
                        }

                        int.TryParse(new string(response.Content.Where(char.IsDigit).ToArray()), out wager);
                        if (wager <= amount) continue;
                        wager = -1;
                        await dm.SendErrorAsync($"You cannot wager more than your score.\nThe maximum you can wager is: `${amount:N0}`")
                            .ConfigureAwait(false);
                        response = await ReplyHandler(dm.Id, true).ConfigureAwait(false);
                    }

                    await dm.EmbedAsync(new EmbedBuilder().WithColor(Color)
                            .WithAuthor("Final Jeopardy!")
                            .WithDescription(
                                $"You successfully wagered `${wager:N0}`\nPlease wait until all other participants have submitted their wager."))
                        .ConfigureAwait(false);

                    Users.AddOrUpdate(user, -wager, (u, old) => old - wager);
                    _confirmed.TryTake(out bool _);
                    while (!_confirmed.IsEmpty) await Task.Delay(1000).ConfigureAwait(false);

                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    await dm.EmbedAsync(new EmbedBuilder().WithColor(Color)
                            .WithAuthor("Final Jeopardy!")
                            .WithTitle($"{_finalJeopardy.Name}")
                            .WithDescription(_finalJeopardy.Clues.Single().Text.EscapeMarkdown() + "\n\nYou have 60 seconds to answer your question. You can submit as many times as you want, however, only the LATEST message is recorded.")
                            .WithFooter($"Your wager is ${wager:N0}. Submit your answer now."))
                        .ConfigureAwait(false);

                    _finalJeopardyAnswers.Add(user.Id, $"{user}: `${wager:N0}` - `No Answer`");
                    var cancel = new CancellationTokenSource();
                    try
                    {
                        _client.MessageReceived += FinalJeopardyGuessHandler;
                        await Task.Delay(TimeSpan.FromSeconds(60), cancel.Token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        //
                    }
                    finally
                    {
                        _client.MessageReceived -= FinalJeopardyGuessHandler;
                        await dm.EmbedAsync(new EmbedBuilder().WithColor(Color)
                                .WithDescription($"Please return back to {_channel.Mention} for final results."))
                            .ConfigureAwait(false);
                    }

                    Task FinalJeopardyGuessHandler(SocketMessage msg)
                    {
                        Task __ = Task.Run(async () =>
                        {
                            try
                            {
                                if (msg.Author.IsBot || msg.Channel.Id != dm.Id || !Regex.IsMatch(msg.Content.ToLowerInvariant(), "^what|where|who")) return;
                                var guess = false;
                                if (_finalJeopardy.Clues.Single().CheckAnswer(msg.Content) && !cancel.IsCancellationRequested)
                                {
                                    Users.AddOrUpdate(user, wager * 2, (u, old) => old + wager * 2);
                                    _finalJeopardyAnswers[user.Id] = $"{user.Username}: `${wager:N0}` - {msg.Content}";
                                    guess = true;
                                }

                                if (!guess)
                                {
                                    _finalJeopardyAnswers[user.Id] = $"{user.Username}: `${wager:N0}` - {msg.Content.TrimTo(100)}";
                                    return;
                                }

                                cancel.Cancel();

                                await dm.EmbedAsync(new EmbedBuilder().WithColor(Color)
                                        .WithAuthor("Final Jeopardy!")
                                        .WithTitle($"{_finalJeopardy.Name}")
                                        .WithDescription($"{msg.Author.Mention} Correct.\nThe correct answer was:\n`{_finalJeopardy.Clues.Single().Answer}`\n" +
                                                         $"Your total score is: `{Users[user]:N0}`"))
                                    .ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                Logger.Warn(e);
                            }
                        }, cancel.Token);

                        return Task.CompletedTask;
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn(e);
                }
            });

            return Task.CompletedTask;
        }

        private async Task<SocketMessage> ReplyHandler(ulong channelId, bool isFinal = false, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(45);
            var eventTrigger = new TaskCompletionSource<SocketMessage>();
            var cancelTrigger = new TaskCompletionSource<bool>();

            Task Handler(SocketMessage message)
            {
                if (message.Channel.Id != channelId || message.Author.IsBot) return Task.CompletedTask;
                string content = message.Content.ToLowerInvariant();
                if (isFinal && Regex.IsMatch(content, "\\d+"))
                {
                    eventTrigger.SetResult(message);
                }
                else if (content.Length >= 5 && Regex.IsMatch(content, " \\d\\d\\d+$"))
                {
                    eventTrigger.SetResult(message);
                }

                return Task.CompletedTask;
            }

            _client.MessageReceived += Handler;

            Task<SocketMessage> trigger = eventTrigger.Task;
            Task<bool> cancel = cancelTrigger.Task;
            Task delay = Task.Delay(timeout.Value);
            Task task = await Task.WhenAny(trigger, cancel, delay).ConfigureAwait(false);

            _client.MessageReceived -= Handler;

            if (task == trigger)
            {
                return await trigger.ConfigureAwait(false);
            }

            return null;
        }

        public string GetLeaderboard()
        {
            if (Users.IsEmpty)
            {
                return "No one is on the leaderboard.";
            }

            var lb = new StringBuilder();
            foreach ((IUser user, int value) in Users.OrderByDescending(k => k.Value)) lb.AppendLine($"{user.Username} `{value:N0}` {_config.CurrencyIcon}");

            return lb.ToString();
        }

        public int VoteSkip(ulong userId)
        {
            //  0 success
            // -1 cant vote yet
            // -2 cant vote
            // -3 already voted
            if (!_canVote) return -1;
            if (Users.All(u => u.Key.Id != userId)) return -2;
            if (Votes.Add(userId))
            {
                return 0;
            }

            return -3;
        }

        private Task VoteDelay()
        {
            Task _ = Task.Run(async () =>
            {
                await _voteSkip.WaitAsync();
                if (_canVote)
                {
                    _voteSkip.Release();
                    return;
                }
                
                await Task.Delay(TimeSpan.FromSeconds(12)).ConfigureAwait(false);
                _canVote = true;
                _voteSkip.Release();
            });

            return Task.CompletedTask;
        }

        private enum CategoryStatus
        {
            Success = 0,
            WrongAmount = -1,
            WrongCategory = -2,
            UnavailableClue = -3,
            FalsePositiveInput = -4,
            NoResponse = int.MinValue
        }

        public void Dispose()
        {
            _guess?.Dispose();
            _voteSkip?.Dispose();
            _cancel?.Dispose();
        }
    }
}