using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules
{
    public abstract class RokiTopLevelModule : ModuleBase
    {
        protected RokiTopLevelModule(bool isTopLevelModule = true)
        {
            ModuleTypeName = isTopLevelModule ? GetType().Name : GetType().DeclaringType.Name;
            LowerModuleTypeName = ModuleTypeName.ToLowerInvariant();
            _log = LogManager.GetCurrentClassLogger();
        }

        protected Logger _log { get; }

        public string ModuleTypeName { get; }
        public string LowerModuleTypeName { get; }
        
        protected ICommandContext ctx => Context;


        public async Task<bool> PromptUserConfirmAsync(EmbedBuilder embed)
        {
            embed.WithOkColor()
                .WithFooter("yes/no");

            var msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            try
            {
                var input = await GetUserInputAsync(ctx.User.Id, ctx.Channel.Id).ConfigureAwait(false);
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
            var client = (DiscordSocketClient) ctx.Client;
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

        public TService _service { get; set; }
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