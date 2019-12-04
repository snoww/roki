using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Extensions;

namespace Roki.Modules.Games.Common
{
    public class Jeopardy
    {
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private readonly Dictionary<string, List<JQuestion>> _questions;

        public IGuild Guild { get; }
        public ITextChannel Channel { get; }

        private CancellationTokenSource _cancel;
        
        public JQuestion CurrentQuestion { get; private set; }
        
        public ConcurrentDictionary<IGuildUser, int> Users = new ConcurrentDictionary<IGuildUser, int>();
        
        public bool IsActive { get; private set; }
        public bool StopGame { get; private set; }
        private int _timeout = 0;
        private Dictionary<int, bool> _choices1 = new Dictionary<int, bool> {{200, false},{400, false},{600, false},{800, false},{1000, false}};
        private Dictionary<int, bool> _choices2 = new Dictionary<int, bool> {{200, false},{400, false},{600, false},{800, false},{1000, false}};
        public Jeopardy(DbService db, DiscordSocketClient client, Dictionary<string, List<JQuestion>> questions, IGuild guild, ITextChannel channel)
        {
            _db = db;
            _client = client;
            _questions = questions;
            
            Guild = guild;
            Channel = channel;
        }

        public async Task StartGame()
        {
            
        }

        private async Task ShowCategories()
        {
            var embed = new EmbedBuilder().WithColor(Color.Blue)
                .WithTitle("Jeopardy!")
                .WithDescription("Welcome to Jeopardy!\nPlease choose a category and price.")
                .AddField(_questions.First().Key, string.Join("\n", _choices1.Select(c => $"${c.Key}")))
                .AddField(_questions.Last().Key, string.Join("\n", _choices2.Select(c => $"${c.Key}")));

            await Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
    }
}