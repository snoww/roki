using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Roki.Common
{
    public sealed class ReactionEventWrapper : IDisposable
    {
        private readonly DiscordSocketClient _client;
        private bool _disposing;

        public ReactionEventWrapper(DiscordSocketClient client, IUserMessage message)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            _client = client;

            _client.ReactionAdded += ReactionAdded;
            _client.ReactionRemoved += ReactionRemoved;
            _client.ReactionsCleared += ReactionsCleared;
        }

        public IUserMessage Message { get; }

        public void Dispose()
        {
            if (_disposing)
                return;
            _disposing = true;
            UnsubAll();
        }

        public event Action<SocketReaction> OnReactionAdded = delegate { };
        public event Action<SocketReaction> OnReactionRemoved = delegate { };
        public event Action OnReactionCleared = delegate { };

        private Task ReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            Task.Run(() =>
            {
                try
                {
                    if (message.Id == Message.Id)
                        OnReactionAdded?.Invoke(reaction);
                }
                catch
                {
                    //
                }
            });

            return Task.CompletedTask;
        }

        private Task ReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            Task.Run(() =>
            {
                try
                {
                    if (message.Id == Message.Id)
                        OnReactionRemoved?.Invoke(reaction);
                }
                catch
                {
                    //
                }
            });

            return Task.CompletedTask;
        }

        private Task ReactionsCleared(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel)
        {
            Task.Run(() =>
            {
                try
                {
                    if (message.Id == Message.Id)
                        OnReactionCleared?.Invoke();
                }
                catch
                {
                    //
                }
            });

            return Task.CompletedTask;
        }

        public void UnsubAll()
        {
            _client.ReactionAdded -= ReactionAdded;
            _client.ReactionRemoved -= ReactionRemoved;
            _client.ReactionsCleared -= ReactionsCleared;
            OnReactionAdded = null;
            OnReactionRemoved = null;
            OnReactionCleared = null;
        }
    }
}