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

        public async Task<bool> PromptUserConfirmAsync(EmbedBuilder embed)
        {
            embed.WithOkColor()
                .WithFooter("yes/no");

            var msg = await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            try
            {
                var input = await GetUserInputAsync(Context.User.Id, Context.Channel.Id).ConfigureAwait(false);
                input = input?.ToUpperInvariant();

                if (input != "YES" && input != "Y") return false;

                return true;
            }
            finally
            {
                var _ = Task.Run(() => msg.DeleteAsync());
            }
        }

        public async Task<string> GetUserInputAsync(ulong userId, ulong channelId)
        {
            var userInputTask = new TaskCompletionSource<string>();
            var client = (DiscordSocketClient) Context.Client;
            try
            {
                client.MessageReceived += MessageReceived;
                if (await Task.WhenAny(userInputTask.Task, Task.Delay(10000)).ConfigureAwait(false) != userInputTask.Task) return null;

                return await userInputTask.Task.ConfigureAwait(false);
            }
            finally
            {
                client.MessageReceived -= MessageReceived;
            }

            Task MessageReceived(SocketMessage arg)
            {
                var _ = Task.Run(() =>
                {
                    if (!(arg is SocketUserMessage userMessage) || !(userMessage.Channel is ITextChannel channel) ||
                        userMessage.Author.Id != userId || userMessage.Channel.Id != channelId) return Task.CompletedTask;

                    if (userInputTask.TrySetResult(arg.Content)) userMessage.DeleteAfter(1);

                    return Task.CompletedTask;
                });
                return Task.CompletedTask;
            }
        }
    }

    public abstract class RokiTopLevelModule<TService> : RokiTopLevelModule where TService : IRokiService
    {
        protected RokiTopLevelModule(bool isTopLevel = true) : base(isTopLevel)
        {
        }

        protected TService Service { get; set; }
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