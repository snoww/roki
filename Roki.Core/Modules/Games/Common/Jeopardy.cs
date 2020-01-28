using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore.Internal;
using NLog;
using Roki.Common;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Games.Common
{
    public class Jeopardy
    {
        private readonly SemaphoreSlim _guess = new SemaphoreSlim(1, 1);
        private readonly ICurrencyService _currency;
        private readonly Logger _log;
        private readonly DiscordSocketClient _client;
        private readonly Dictionary<string, List<JClue>> _clues;

        private readonly IGuild _guild;
        private readonly ITextChannel _channel;

        private CancellationTokenSource _cancel;
        
        public JClue CurrentClue { get; private set; }
        private readonly JClue _finalJeopardy;
        private readonly Dictionary<ulong, string> _finalJeopardyAnswers = new Dictionary<ulong, string>();

        public readonly ConcurrentDictionary<IUser, int> Users = new ConcurrentDictionary<IUser, int>();
        private readonly ConcurrentBag<bool> _confirmed = new ConcurrentBag<bool>();

        private bool _canGuess;
        private bool _stopGame;
        private int _guessCount;

        public HashSet<ulong> Votes { get; } = new HashSet<ulong>();
        private bool _canVote;

        public readonly Color Color = Color.DarkBlue;
        
        public Jeopardy(DiscordSocketClient client, Dictionary<string, List<JClue>> clues, IGuild guild, ITextChannel channel, ICurrencyService currency, JClue finalJeopardy)
        {
            _log = LogManager.GetCurrentClassLogger();
            _client = client;
            _clues = clues;
            
            _guild = guild;
            _channel = channel;
            _currency = currency;
            _finalJeopardy = finalJeopardy;
        }

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
                var catResponse = await ReplyHandler(_channel.Id, timeout: TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                var catStatus = ParseCategoryAndClue(catResponse);
                while (catStatus != CategoryStatus.Success)
                {
                    if (catStatus == CategoryStatus.UnavailableClue)
                        await _channel.SendErrorAsync("That clue is not available.\nPlease try again.").ConfigureAwait(false);
                    else if (catStatus == CategoryStatus.WrongAmount)
                        await _channel.SendErrorAsync("There are no clues available for that amount.\nPlease try again.")
                            .ConfigureAwait(false);
                    else if (catStatus == CategoryStatus.WrongCategory)
                        await _channel.SendErrorAsync("No such category found.\nPlease try again.").ConfigureAwait(false);
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
            var msg = await _channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Jeopardy!")
                    .WithTitle("Final Winnings")
                    .WithDescription(GetLeaderboard()))
                .ConfigureAwait(false);

            if (!Users.Any()) return;
            foreach (var (user, winnings) in Users)
            {
                await _currency.AddAsync(user.Id, "Jeopardy Winnings", winnings, _guild.Id, _channel.Id, msg.Id)
                    .ConfigureAwait(false);
            }
        }

        public async Task StopJeopardyGame()
        {
            var old = _stopGame;
            _stopGame = true;
            if (!old)
                await _channel.SendErrorAsync("Jeopardy! game stopping after this question.").ConfigureAwait(false);
        }

        private bool AvailableClues()
        {
            return _clues.Values.Any(clues => clues.Any(c => c.Available));
        }
        
        private async Task ShowCategories()
        {
            var embed = new EmbedBuilder().WithColor(Color)
                .WithTitle("Jeopardy!")
                .WithDescription($"Please choose an available category and price from below.\ni.e. `{_clues.First().Key} for 200`");
            foreach (var (category, clues) in _clues)
            {
                embed.AddField(category, string.Join("\n", clues.Select(c => $"{(c.Available ? $"`${c.Value}`" : $"~~`${c.Value}`~~")}")), true);
            }
            
            await _channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private CategoryStatus ParseCategoryAndClue(IMessage msg)
        {
            if (msg == null) return CategoryStatus.NoResponse;
            
            var message = msg.Content.SanitizeStringFull().ToLowerInvariant();
            var category = message.Substring(0, message.LastIndexOf("for", StringComparison.OrdinalIgnoreCase));
            var price = message.Substring(message.LastIndexOf("for", StringComparison.OrdinalIgnoreCase));
            int.TryParse(new string(price.Where(char.IsDigit).ToArray()), out var amount);

            JClue clue = null;
            foreach (var (cat, clues) in _clues)
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
            var _ = Task.Run(async () =>
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
                    _log.Warn(e);
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
                
            foreach (var (user, amount) in Users)
            {
                _confirmed.Add(true);
                await DmFinalJeopardy(user, amount).ConfigureAwait(false);
            }
                
            while (!_confirmed.IsEmpty)
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }
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
            var _ = Task.Run(async () =>
            {
                try
                {
                    var dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                    await dm.EmbedAsync(new EmbedBuilder().WithColor(Color)
                            .WithAuthor("Final Jeopardy!")
                            .WithTitle(_finalJeopardy.Category)
                            .WithDescription($"Please make your wager. You're current score is: `${amount:N0}`"))
                        .ConfigureAwait(false);

                    var response = await ReplyHandler(dm.Id, true).ConfigureAwait(false);
                    var wager = -1;
                    while (wager == -1)
                    {
                        if (response == null)
                        {
                            await dm.SendErrorAsync("No response received. You are removed from the Final Jeopardy!").ConfigureAwait(false);
                            _confirmed.TryTake(out var _);
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
                    _confirmed.TryTake(out var _);
                    while (!_confirmed.IsEmpty)
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                    }

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
                        var __ = Task.Run(async () =>
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
                                _log.Warn(e);
                            }
                        });

                        return Task.CompletedTask;
                    }
                }
                catch (Exception e)
                {
                    _log.Warn(e);
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
                var content = message.Content.SanitizeStringFull().ToLowerInvariant();
                if (isFinal && Regex.IsMatch(content, "\\d+")) 
                    eventTrigger.SetResult(message);
                if (content.Contains("for", StringComparison.Ordinal) && Regex.IsMatch(content, "\\d\\d\\d+"))
                    eventTrigger.SetResult(message);

                return Task.CompletedTask;
            }
            
            _client.MessageReceived += Handler;

            var trigger = eventTrigger.Task;
            var cancel = cancelTrigger.Task;
            var delay = Task.Delay(timeout.Value);
            var task = await Task.WhenAny(trigger, cancel, delay).ConfigureAwait(false);

            _client.MessageReceived -= Handler;

            if (task == trigger)
                return await trigger.ConfigureAwait(false);
            return null;
        }

        public string GetLeaderboard()
        {
            if (Users.Count == 0)
                return "No one is on the leaderboard.";
            
            var lb = new StringBuilder();
            foreach (var (user, value) in Users.OrderByDescending(k => k.Value))
            {
                lb.AppendLine($"{user.Username} `{value:N0}` {Roki.Services.Roki.Properties.CurrencyIcon}");
            }

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
                return 0;
            return -3;
        }

        private Task VoteDelay()
        {
            var _ = Task.Run(async () =>
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
            NoResponse = int.MinValue, 
        }
    }
}