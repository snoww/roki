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
        private SemaphoreSlim _guess = new SemaphoreSlim(1, 1);
        private readonly ICurrencyService _currency;
        private readonly Logger _log;
        private readonly DiscordSocketClient _client;
        private readonly Dictionary<string, List<JClue>> _clues;
        private readonly Roki _roki;

        private IGuild Guild { get; }
        private ITextChannel Channel { get; }

        private CancellationTokenSource _cancel;
        
        public JClue CurrentClue { get; private set; }
        private JClue FinalJeopardy { get; set; }
        private readonly List<string> _finalJeopardyAnswers = new List<string>();

        private readonly ConcurrentDictionary<IUser, int> _users = new ConcurrentDictionary<IUser, int>();
        private readonly ConcurrentBag<bool> _confirmed = new ConcurrentBag<bool>();

        private bool CanGuess { get; set; }
        private bool StopGame { get; set; }
        public readonly Color Color = Color.DarkBlue;
        
        public Jeopardy(DiscordSocketClient client, Dictionary<string, List<JClue>> clues, IGuild guild, ITextChannel channel, Roki roki, 
            ICurrencyService currency, JClue finalJeopardy = null)
        {
            _log = LogManager.GetCurrentClassLogger();
            _client = client;
            _clues = clues;
            
            Guild = guild;
            Channel = channel;
            _roki = roki;
            _currency = currency;
            FinalJeopardy = finalJeopardy;
        }

        public async Task StartGame()
        {
            await Channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithTitle("Jeopardy!")
                    .WithDescription("Welcome to Jeopardy!\nGame is starting soon...")
                    .WithFooter("Responses must be in question form"))
                .ConfigureAwait(false);
            await Channel.TriggerTypingAsync().ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            while (!StopGame)
            {
                _cancel = new CancellationTokenSource();
                
                await ShowCategories().ConfigureAwait(false);
                var catResponse = await ReplyHandler(Channel.Id, timeout: TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                var catStatus = ParseCategoryAndClue(catResponse);
                while (catStatus != CategoryStatus.Success)
                {
                    if (catStatus == CategoryStatus.UnavailableClue)
                        await Channel.SendErrorAsync("That clue is not available.\nPlease try again.").ConfigureAwait(false);
                    else if (catStatus == CategoryStatus.WrongAmount)
                        await Channel.SendErrorAsync("There are no clues available for that amount.\nPlease try again.")
                            .ConfigureAwait(false);
                    else if (catStatus == CategoryStatus.WrongCategory)
                        await Channel.SendErrorAsync("No such category found.\nPlease try again.").ConfigureAwait(false);
                    else
                    {
                        await Channel.SendErrorAsync("No response received, stopping Jeopardy! game.").ConfigureAwait(false);
                        return;
                    }
                    
                    catResponse = await ReplyHandler(Channel.Id, timeout: TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                    catStatus = ParseCategoryAndClue(catResponse);
                }
                
                // CurrentClue is now the chosen clue
                await Channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                        .WithAuthor("Jeopardy!")
                        .WithTitle($"{CurrentClue.Category} - ${CurrentClue.Value}")
                        .WithDescription(CurrentClue.Clue))
                    .ConfigureAwait(false);

                try
                {
                    _client.MessageReceived += GuessHandler;
                    CanGuess = true;
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
                    CanGuess = false;
                    _client.MessageReceived -= GuessHandler;
                }

                if (!_cancel.IsCancellationRequested)
                {
                    await Channel.EmbedAsync(new EmbedBuilder().WithColor(Color.Red)
                            .WithAuthor("Jeopardy!")
                            .WithTitle("Times Up!")
                            .WithDescription($"The correct answer was: `{CurrentClue.Answer}`"))
                        .ConfigureAwait(false);
                }
                
                if (!AvailableClues()) break;
                await Task.Delay(TimeSpan.FromSeconds(7)).ConfigureAwait(false);
            }

            if (!StopGame && FinalJeopardy != null)
                await StartFinalJeopardy().ConfigureAwait(false);
        }

        public async Task EnsureStopped()
        {
            StopGame = true;
            var msg = await Channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Jeopardy!")
                    .WithTitle("Final Winnings")
                    .WithDescription(GetLeaderboard()))
                .ConfigureAwait(false);

            if (!_users.Any()) return;
            foreach (var (user, winnings) in _users)
            {
                await _currency.ChangeAsync(user.Id, "Jeopardy Winnings", winnings, _client.CurrentUser.Id, user.Id, Guild.Id, Channel.Id, msg.Id)
                    .ConfigureAwait(false);
            }
        }

        public async Task StopJeopardyGame()
        {
            var old = StopGame;
            StopGame = true;
            if (!old)
                await Channel.SendErrorAsync("Jeopardy! game stopping after this question.").ConfigureAwait(false);
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
            
            await Channel.EmbedAsync(embed).ConfigureAwait(false);
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
                    if (msg.Author.IsBot || msg.Channel != Channel || !Regex.IsMatch(msg.Content.ToLowerInvariant(), "^what|where|who")) return;
                    var guess = false;
                    await _guess.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        if (CanGuess && CurrentClue.CheckAnswer(msg.Content) && !_cancel.IsCancellationRequested)
                        {
                            _users.AddOrUpdate(msg.Author, CurrentClue.Value, (u, old) => old + CurrentClue.Value);
                            guess = true;
                        }
                    }
                    finally
                    {
                        _guess.Release();
                    }
                    if (!guess) return;
                    _cancel.Cancel();

                    await Channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                            .WithAuthor("Jeopardy!")
                            .WithTitle($"{CurrentClue.Category} - ${CurrentClue.Value}")
                            .WithDescription($"{msg.Author.Mention} Correct.\nThe correct answer was: `{CurrentClue.Answer}`\n" +
                                             $"Your total score is: `{_users[msg.Author]:N0}`"))
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
            await Channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Jeopardy!")
                    .WithTitle("Final Jeopardy!")
                    .WithDescription("Check your DMs to play the Final Jeopardy!")
                    .WithFooter("You must have a score to play the Final Jeopardy!"))
                .ConfigureAwait(false);
                
            foreach (var (user, amount) in _users)
            {
                _confirmed.Add(true);
                await DmFinalJeopardy(user, amount).ConfigureAwait(false);
            }
                
            while (!_confirmed.IsEmpty)
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }
                
            await Channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Final Jeopardy!")
                    .WithTitle($"{FinalJeopardy.Category}")
                    .WithDescription(FinalJeopardy.Clue))
                .ConfigureAwait(false);
                
            await Task.Delay(TimeSpan.FromSeconds(35)).ConfigureAwait(false);
                
            await Channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Final Jeopardy!")
                    .WithDescription($"The correct answer is: `{FinalJeopardy.Answer}`"))
                .ConfigureAwait(false);

            await Channel.EmbedAsync(new EmbedBuilder().WithColor(Color)
                    .WithAuthor("Jeopardy!")
                    .WithTitle("Final Jeopardy! results")
                    .WithDescription(string.Join("\n", _finalJeopardyAnswers)))
                .ConfigureAwait(false);
        }

        private Task DmFinalJeopardy(IUser user, int amount)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    var dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                    await dm.EmbedAsync(new EmbedBuilder().WithColor(Color)
                            .WithTitle("Final Jeopardy!")
                            .WithDescription($"Please make your wager. You're current score is: `${amount:N0}`"))
                        .ConfigureAwait(false);

                    var response = await ReplyHandler(dm.Id, true).ConfigureAwait(false);
                    var wager = -1;
                    while (wager != -1)
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
                    }
                    
                    _users.AddOrUpdate(user, -wager, (u, old) => old - wager);
                    _confirmed.TryTake(out var _);
                    while (!_confirmed.IsEmpty)
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                    }

                    await dm.EmbedAsync(new EmbedBuilder().WithColor(Color)
                            .WithAuthor("Final Jeopardy!")
                            .WithTitle($"{FinalJeopardy.Category} - Your wager is `${wager:N0}`")
                            .WithDescription(FinalJeopardy.Clue)
                            .WithFooter("You have 35 seconds to submit your answers!"))
                        .ConfigureAwait(false);

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
                                .WithDescription("Please return back to the channel for final results."))
                            .ConfigureAwait(false);
                    }
                    
                    Task FinalJeopardyGuessHandler(SocketMessage msg)
                    {
                        var __ = Task.Run(async () =>
                        {
                            try
                            {
                                if (msg.Author.IsBot || msg.Channel != dm || !Regex.IsMatch(msg.Content.ToLowerInvariant(), "^what|where|who")) return;
                                var guess = false;
                                if (FinalJeopardy.CheckAnswer(msg.Content) && !cancel.IsCancellationRequested)
                                {
                                    _users.AddOrUpdate(msg.Author, wager * 2, (u, old) => old + wager * 2);
                                    _finalJeopardyAnswers.Add($"{user.Username}: `${wager}` - {msg.Content}");
                                    guess = true;
                                }

                                if (!guess)
                                {
                                    _finalJeopardyAnswers.Add($"{user.Username}: `${wager}` - {msg.Content.TrimTo(100)}");
                                    return;
                                }
                                cancel.Cancel();

                                await dm.EmbedAsync(new EmbedBuilder().WithColor(Color)
                                        .WithAuthor("Final Jeopardy!")
                                        .WithTitle($"{FinalJeopardy.Category} - ${FinalJeopardy.Value}")
                                        .WithDescription($"{msg.Author.Mention} Correct.\nThe correct answer was: `{FinalJeopardy.Answer}`\n" +
                                                         $"Your total score is: `{_users[msg.Author]:N0}`"))
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
            if (_users.Count == 0)
                return "No one is on the leaderboard.";
            
            var lb = new StringBuilder();
            foreach (var (user, value) in _users.OrderByDescending(k => k.Value))
            {
                lb.AppendLine($"{user.Username} `{value:N0}` {_roki.Properties.CurrencyIcon}");
            }

            return lb.ToString();
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