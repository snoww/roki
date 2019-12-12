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
            await _ctx.Channel.EmbedAsync(new EmbedBuilder().WithColor(Color.Blue)
                    .WithTitle("Jeopardy!")
                    .WithDescription(
                        "Welcome to Jeopardy!\nTo choose a category, please use the format `category for xxx`, you must specify full category name.\nResponses must be in question form"))
                .ConfigureAwait(false);
            while (!StopGame)
            {
                _cancel = new CancellationTokenSource();

                await ShowCategories().ConfigureAwait(false);
                var category = await ReplyHandler(ReplyType.Category).ConfigureAwait(false);
                
            }

        }

        private async Task ShowCategories()
        {
            var embed = new EmbedBuilder().WithColor(Color.Blue)
                .WithTitle("Jeopardy!")
                .WithDescription($"Please choose an available category and price from below.\ni.e. `{_questions.First().Key} for 200`");
            foreach (var (category, questions) in _questions)
            {
                embed.AddField(category, string.Join("\n", questions.Select(c => $"${(c.Available ? $"`{c.Value}`" : $"~~`{c.Value}`~~")}")));
            }
            
            await Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private CategoryStatus ParseCategory(string message)
        {
            int.TryParse(new string(message.Where(char.IsDigit).ToArray()), out var amount);

            JQuestion question;
            if (message.Contains(_questions.First().Key, StringComparison.OrdinalIgnoreCase))
                question = _questions.First().Value.FirstOrDefault(q => q.Value == amount);
            else if (message.Contains(_questions.First().Key, StringComparison.OrdinalIgnoreCase))
                question = _questions.First().Value.FirstOrDefault(q => q.Value == amount);
            else
                return CategoryStatus.WrongCategory;
            
            if (question == null) return CategoryStatus.WrongAmount;
            if (!question.Available) return CategoryStatus.UnavailableQuestion;
            CurrentQuestion = question;
            return CategoryStatus.Success;
        }
        
        private async Task<SocketMessage> ReplyHandler(ReplyType type, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromMinutes(2);
            var eventTrigger = new TaskCompletionSource<SocketMessage>();
            var cancelTrigger = new TaskCompletionSource<bool>();

            Task Handler(SocketMessage message)
            {
                if (message.Channel.Id != _ctx.Channel.Id || message.Author.IsBot) return Task.CompletedTask;
                var content = message.Content.SanitizeStringFull().ToLowerInvariant();
                if (type == ReplyType.Category && !content.Contains("for", StringComparison.Ordinal) && !Regex.IsMatch(content, "\\d\\d\\d+"))
                    return Task.CompletedTask;
                if (type == ReplyType.Guess && !Regex.IsMatch(content, "^what|where|who")) return Task.CompletedTask;

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
        
        private enum CategoryStatus
        {
            Success = 0,
            WrongAmount = -1,
            WrongCategory = -2,
            UnavailableQuestion = -3
        }
    }
}