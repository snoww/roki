using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Discord;
using Discord.WebSocket;
using Roki.Core.Services;

namespace Roki.Modules.Games.Common
{
    public class Jeopardy
    {
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private readonly Dictionary<string, List<JQuestion>> _questions;

        public IGuild Guild { get; }
        public ITextChannel Channel { get; }

        private CancellationTokenSource _cancellation;
        
        public JQuestion CurrentQuestion { get; private set; }
        
        public ConcurrentDictionary<IGuildUser, int> Users = new ConcurrentDictionary<IGuildUser, int>();
        
        public bool IsActive { get; private set; }
        public bool StopGame { get; private set; }
        private int _timeout = 0;
        
        public Jeopardy(DbService db, DiscordSocketClient client, Dictionary<string, List<JQuestion>> questions, IGuild guild, ITextChannel channel)
        {
            _db = db;
            _client = client;
            _questions = questions;
            
            Guild = guild;
            Channel = channel;
        }
    }
}