using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Currency.Services;

namespace Roki.Modules.Currency
{
    public partial class Currency
    {
        public class PickDropCommands : RokiSubmodule<PickDropService>
        {
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Pick()
            {
                var picked = await _service.PickAsync(ctx.Guild.Id, (ITextChannel) ctx.Channel, ctx.User).ConfigureAwait(false);

                if (picked > 0)
                {
                    IUserMessage msg;
                    if (picked == 1)
                    {
                        msg = await ctx.Channel.SendMessageAsync("You picked up 1 stone.").ConfigureAwait(false);
                    }
                    else
                    {
                        msg = await ctx.Channel.SendMessageAsync($"You picked up {picked} stones.").ConfigureAwait(false);
                    }

                    msg.DeleteAfter(10);
                }

                try
                {
                    await ctx.Message.DeleteAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    //
                }
            }
        }
    }
}