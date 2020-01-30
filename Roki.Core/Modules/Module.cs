using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules
{
    public abstract class RokiTopLevelModule : ModuleBase
    {
        private string ModuleTypeName { get; }
        private string LowerModuleTypeName { get; }
        protected Logger Log { get; }
        
        protected RokiTopLevelModule(bool isTopLevelModule = true)
        {
            ModuleTypeName = isTopLevelModule ? GetType().Name : GetType().DeclaringType?.Name;
            LowerModuleTypeName = ModuleTypeName?.ToLowerInvariant();
            Log = LogManager.GetCurrentClassLogger();
        }

        public async Task<SocketMessage> GetUserReply(TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromMinutes(5);
            var eventTrigger = new TaskCompletionSource<SocketMessage>();

            Task Handler(SocketMessage message)
            {
                if (message.Channel.Id != Context.Channel.Id || message.Author.Id != Context.User.Id) return Task.CompletedTask;
                eventTrigger.SetResult(message);
                return Task.CompletedTask;
            }

            var client = (DiscordSocketClient) Context.Client;
            client.MessageReceived += Handler;

            var trigger = eventTrigger.Task;
            var delay = Task.Delay(timeout.Value);
            var task = await Task.WhenAny(trigger, delay).ConfigureAwait(false);

            client.MessageReceived -= Handler;

            if (task == trigger)
                return await trigger.ConfigureAwait(false);
            return null;
        }
    }

    public abstract class RokiTopLevelModule<TService> : RokiTopLevelModule where TService : IRokiService
    {
        protected RokiTopLevelModule(bool isTopLevel = true) : base(isTopLevel)
        {
        }

        public TService Service { get; set; }
    }

    public abstract class RokiSubmodule : RokiTopLevelModule
    {
        protected RokiSubmodule() : base(false)
        {
        }
    }

    public abstract class RokiSubmodule<TService> : RokiTopLevelModule<TService> where TService : IRokiService
    {
        protected RokiSubmodule() : base(false)
        {
        }
    }
}