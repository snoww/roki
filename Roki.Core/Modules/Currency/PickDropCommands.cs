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
        [Group]
        public class PickDropCommands : RokiSubmodule<PickDropService>
        {
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Pick()
            {
                await ctx.Message.DeleteAsync().ConfigureAwait(false);
                var picked = await _service.PickAsync((ITextChannel) ctx.Channel, ctx.User).ConfigureAwait(false);

                if (picked > 0)
                {
                    IUserMessage msg;
                    if (picked == 1)
                        msg = await ctx.Channel.SendMessageAsync($"{ctx.User.Username} picked up 1 {Roki.Properties.CurrencyName}.").ConfigureAwait(false);
                    else
                        msg = await ctx.Channel.SendMessageAsync($"{ctx.User.Username} picked up {picked:N0} {Roki.Properties.CurrencyNamePlural}.").ConfigureAwait(false);

                    msg.DeleteAfter(10);
                }
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Drop([Leftover] long amount = 1)
            {
                ctx.Message.DeleteAfter(10);
                if (amount < 0)
                    return;
                
                var success = await _service.DropAsync(ctx, ctx.User, amount).ConfigureAwait(false);

                if (!success)
                    await ctx.Channel.SendMessageAsync($"You do not have enough {Roki.Properties.CurrencyIcon} to drop.").ConfigureAwait(false);

            }
        }
    }
}