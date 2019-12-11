using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore.Internal;
using Roki.Common;
using Roki.Core.Services;
using Roki.Extensions;

namespace Roki.Modules.Games.Common
{
    public class Jeopardy
    {
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private readonly ICommandContext _ctx;
        private readonly Dictionary<string, List<JQuestion>> _questions;

        public IGuild Guild { get; }
        public ITextChannel Channel { get; }

        private CancellationTokenSource _cancel;
        
        public JQuestion CurrentQuestion { get; private set; }
        
        public ConcurrentDictionary<IGuildUser, int> Users = new ConcurrentDictionary<IGuildUser, int>();
        
        public bool IsActive { get; private set; }
        public bool StopGame { get; private set; }
        private int _timeout = 0;
//        private Dictionary<int, bool> _choices1 = new Dictionary<int, bool> {{200, false},{400, false},{600, false},{800, false},{1000, false}};
//        private Dictionary<int, bool> _choices2 = new Dictionary<int, bool> {{200, false},{400, false},{600, false},{800, false},{1000, false}};
        
        public Jeopardy(DbService db, DiscordSocketClient client, Dictionary<string, List<JQuestion>> questions, IGuild guild, ITextChannel channel, ICommandContext ctx)
        {
            _db = db;
            _client = client;
            _questions = questions;
            
            Guild = guild;
            Channel = channel;
            _ctx = ctx;
        }

        public async Task StartGame()
        {
            await ShowCategories().ConfigureAwait(false);
            _cancel = new CancellationTokenSource();
            var reply = await ReplyHandler().ConfigureAwait(false);
            if (reply == null)
            {
                await _ctx.Channel.SendErrorAsync("No reply received. Stopping Jeopardy game.").ConfigureAwait(false);
                return;
            }

            var (question, status) = ParseQuestionChoice(reply.Content);
            while (status != ChoiceStatus.Success)
            {
                if (status == ChoiceStatus.NoAmount) 
                await _ctx.Channel.SendErrorAsync("Invalid choice, please try again.").ConfigureAwait(false);
                reply = await ReplyHandler(TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                if (reply == null)
                {
                    await _ctx.Channel.SendErrorAsync("No reply received. Stopping Jeopardy game.").ConfigureAwait(false);
                    return;
                }

                (question, status) = ParseQuestionChoice(reply.Content);
            }

            await _ctx.Channel.EmbedAsync(new EmbedBuilder().WithColor(Color.Blue)
                    .WithTitle($"{question.Category} - ${question.Value}")
                    .WithDescription(question.Clue))
                .ConfigureAwait(false);

        }

        private async Task ShowCategories()
        {
            var embed = new EmbedBuilder().WithColor(Color.Blue)
                .WithTitle("Jeopardy!")
                .WithDescription($"Welcome to Jeopardy!\nPlease choose a category and price.\ni.e. `{_questions.First().Key} for 200`")
                .AddField(_questions.First().Key, string.Join("\n", _questions.First().Value.Select(c => $"${c.Value}")))
                .AddField(_questions.Last().Key, string.Join("\n", _questions.Last().Value.Select(c => $"${c.Value}")));

            await Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
        

        private (JQuestion, ChoiceStatus) ParseQuestionChoice(string message)
        {
            var numStr = Regex.Match(message, @"\d+").Value;
            if (string.IsNullOrWhiteSpace(numStr) || !int.TryParse(numStr, out var amount)) return (null, ChoiceStatus.NoAmount);

            JQuestion ques;
            if (message.Contains(_questions.First().Key, StringComparison.OrdinalIgnoreCase))
            {
                ques = _questions.First().Value.FirstOrDefault(q => q.Value == amount && q.Available);
                if (ques == null) return (null, ChoiceStatus.WrongAmount);
                ques.Available = false;
                return (ques, ChoiceStatus.Success);

            }
            if (message.Contains(_questions.Last().Key, StringComparison.OrdinalIgnoreCase))
            {
                ques = _questions.Last().Value.FirstOrDefault(q => q.Value == amount && q.Available);
                if (ques == null) return (null, ChoiceStatus.WrongAmount);
                ques.Available = false;
                return (ques, ChoiceStatus.Success);
            }

            return (null, ChoiceStatus.WrongCategory);
        }
        
        private async Task<SocketMessage> ReplyHandler(ReplyType type, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromMinutes(2);
            var eventTrigger = new TaskCompletionSource<SocketMessage>();
            var cancelTrigger = new TaskCompletionSource<bool>();

            Task Handler(SocketMessage message)
            {
                if (message.Channel.Id != _ctx.Channel.Id || message.Author.IsBot) return Task.CompletedTask;
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
        
        private enum ReplyType
        {
            Category,
            Guess
        }
        
        private enum ChoiceStatus
        {
            Success = 0,
            NoAmount = -1,
            WrongAmount = -1,
            WrongCategory = -2,
        }
    }
}