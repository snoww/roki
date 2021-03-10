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
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Models;

namespace Roki.Modules.Games.Common
{
    public class Jeopardy
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ITextChannel _channel;
        private readonly ICurrencyService _currency;
        private readonly DiscordSocketClient _client;
        private readonly GuildConfig _config;
        private readonly Dictionary<string, List<JClue>> _clues;
        private readonly ConcurrentBag<bool> _confirmed = new();
        private readonly JClue _finalJeopardy;
        private readonly Dictionary<ulong, string> _finalJeopardyAnswers = new();
        private readonly SemaphoreSlim _guess = new(1, 1);

        private readonly IGuild _guild;

        public readonly Color Color = Color.DarkBlue;

        public readonly ConcurrentDictionary<IUser, int> Users = new();

        private CancellationTokenSource _cancel;

        private bool _canGuess;
        private bool _canVote;
        private int _guessCount;
        private bool _stopGame;

        public Jeopardy(DiscordSocketClient client, GuildConfig config, Dictionary<string, List<JClue>> clues, IGuild guild, ITextChannel channel, ICurrencyService currency, JClue finalJeopardy)
        {
            _client = client;
            _config = config;
            _clues = clues;
            _guild = guild;
            _channel = channel;
            _currency = currency;
            _finalJeopardy = finalJeopardy;
        }

        public JClue CurrentClue { get; private set; }

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
                        await _channel.SendErrorAsync("That clue is not available.\nPlease try again.").ConfigureAwait(false);
                    }
                    else if (catStatus == CategoryStatus.WrongAmount)
                    {
                        await _channel.SendErrorAsync("There are no clues available for that amount.\nPlease try again.")
                            .ConfigureAwait(false);
                    }
                    else if (catStatus == CategoryStatus.WrongCategory)
                    {
                        await _channel.SendErrorAsync("No such category found.\nPlease try again.").ConfigureAwait(false);
                    }
                    else
                    {
                        await _channel.SendErrorAsync("No response received, stopping Jeopardy! game.").ConfigureAwait(false);
                        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        return;
                    }

                    catResponse = await ReplyHandler(_channel.Id, timeout: TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                    catStatus = ParseCategoryAndClue(catResponse);
                }

                // CurrentClue is now the chosen clue
                await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                        .WithAuthor("Jeopardy!")
                        .WithTitle($"{CurrentClue.Category} - ${CurrentClue.Value}")
                        .WithDescription(CurrentClue.Clue))
                    .ConfigureAwait(false);

                try
                {
                    _client.MessageReceived += GuessHandler;
                    _canGuess = true;
                    await VoteDelay().ConfigureAwait(false);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(35), _cancel.Token).ConfigureAwait(false);
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

                if (!AvailableClues()) break;
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

        private bool AvailableClues()
        {
            return _clues.Values.Any(clues => clues.Any(c => c.Available));
        }

        private async Task ShowCategories()
        {
            EmbedBuilder embed = new EmbedBuilder().WithColor(Color)
                .WithTitle("Jeopardy!")
                .WithDescription($"Please choose an available category and price from below.\ni.e. `{_clues.First().Key} for 200`");
            foreach ((string category, List<JClue> clues) in _clues) embed.AddField(category, string.Join("\n", clues.Select(c => $"{(c.Available ? $"`${c.Value}`" : $"~~`${c.Value}`~~")}")), true);

            await _channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private CategoryStatus ParseCategoryAndClue(IMessage msg)
        {
            if (msg == null) return CategoryStatus.NoResponse;

            string message = msg.Content.SanitizeStringFull().ToLowerInvariant();
            string category = message[..message.LastIndexOf("for", StringComparison.OrdinalIgnoreCase)];
            string price = message[message.LastIndexOf("for", StringComparison.OrdinalIgnoreCase)..];
            if (!int.TryParse(new string(price.Where(char.IsDigit).ToArray()), out int amount))
            {
                // shouldn't ever reach here, since message is retrieved from regex pattern which needs a number
                return CategoryStatus.WrongAmount;
            }

            JClue clue = null;
            foreach ((string cat, List<JClue> clues) in _clues)
            {
                if (!cat.SanitizeStringFull().ToLowerInvariant().Contains(category, StringComparison.Ordinal)) continue;
                clue = clues.FirstOrDefault(q => q.Value == amount);
                if (clue == null) return CategoryStatus.WrongAmount;
                break;
            }

            if (clue == null) return CategoryStatus.WrongCategory;
            if (!clue.Available) return CategoryStatus.UnavailableClue;
            CurrentClue = clue;
            CurrentClue.Available = false;
            return CategoryStatus.Success;
        }

        private Task GuessHandler(SocketMessage msg)
        {
            Task _ = Task.Run(async () =>
            {
                try
                {
                    if (msg.Channel.Id != _channel.Id) return;
                    if (_canVote && Users.Count != 0 && Votes.Count >= Users.Count)
                    {
                        Votes.Clear();
                        _cancel.Cancel();
                        await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                                .WithAuthor("Jeopardy!")
                                .WithTitle($"{CurrentClue.Category} - ${CurrentClue.Value}")
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
                                    .WithTitle($"{CurrentClue.Category} - ${CurrentClue.Value}")
                                    .WithDescription(CurrentClue.Clue))
                                .ConfigureAwait(false);
                        }

                        return;
                    }

                    _cancel.Cancel();
                    await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                            .WithAuthor("Jeopardy!")
                            .WithTitle($"{CurrentClue.Category} - ${CurrentClue.Value}")
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
                    .WithFooter("You must have a score to participate in the Final Jeopardy!"))
                .ConfigureAwait(false);

            foreach ((IUser user, int amount) in Users)
            {
                _confirmed.Add(true);
                await DmFinalJeopardy(user, amount).ConfigureAwait(false);
            }

            while (!_confirmed.IsEmpty) await Task.Delay(1000).ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Final Jeopardy!")
                    .WithTitle($"{_finalJeopardy.Category}")
                    .WithDescription(_finalJeopardy.Clue)
                    .WithFooter("Note: your answers will not be checked here"))
                .ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromSeconds(35)).ConfigureAwait(false);

            await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Final Jeopardy!")
                    .WithDescription($"The correct answer is:\n`{_finalJeopardy.Answer}`"))
                .ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);

            await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Final Jeopardy!")
                    .WithTitle("Results")
                    .WithDescription(_finalJeopardyAnswers.Count > 0 ? string.Join("\n", _finalJeopardyAnswers.Values) : "No wagers."))
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
                            .WithTitle(_finalJeopardy.Category)
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
                            .WithTitle($"{_finalJeopardy.Category}")
                            .WithDescription(_finalJeopardy.Clue)
                            .WithFooter($"Your wager is ${wager:N0}. Submit your answer now."))
                        .ConfigureAwait(false);

                    _finalJeopardyAnswers.Add(user.Id, $"{user.Username}: `${wager:N0}` - `No Answer`");
                    var cancel = new CancellationTokenSource();
                    try
                    {
                        _client.MessageReceived += FinalJeopardyGuessHandler;
                        await Task.Delay(TimeSpan.FromSeconds(35), cancel.Token).ConfigureAwait(false);
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
                                if (_finalJeopardy.CheckAnswer(msg.Content) && !cancel.IsCancellationRequested)
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
                                        .WithTitle($"{_finalJeopardy.Category}")
                                        .WithDescription($"{msg.Author.Mention} Correct.\nThe correct answer was:\n`{_finalJeopardy.Answer}`\n" +
                                                         $"Your total score is: `{Users[user]:N0}`"))
                                    .ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                Logger.Warn(e);
                            }
                        });

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
            timeout ??= TimeSpan.FromSeconds(35);
            var eventTrigger = new TaskCompletionSource<SocketMessage>();
            var cancelTrigger = new TaskCompletionSource<bool>();

            Task Handler(SocketMessage message)
            {
                if (message.Channel.Id != channelId || message.Author.IsBot) return Task.CompletedTask;
                string content = message.Content.SanitizeStringFull().ToLowerInvariant();
                if (isFinal && Regex.IsMatch(content, "\\d+"))
                {
                    eventTrigger.SetResult(message);
                }

                if (content.Contains("for", StringComparison.Ordinal) && Regex.IsMatch(content, "\\d\\d\\d+"))
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
                await Task.Delay(TimeSpan.FromSeconds(12)).ConfigureAwait(false);
                _canVote = true;
            });

            return Task.CompletedTask;
        }

        private enum CategoryStatus
        {
            Success = 0,
            WrongAmount = -1,
            WrongCategory = -2,
            UnavailableClue = -3,
            NoResponse = int.MinValue
        }
    }
}