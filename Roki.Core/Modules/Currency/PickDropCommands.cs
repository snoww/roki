using System.Threading.Tasks;
using Discord;
using Discord.Commands;
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
                await Context.Message.DeleteAsync().ConfigureAwait(false);
                long picked = await Service.PickAsync((ITextChannel) Context.Channel, Context.User).ConfigureAwait(false);

                if (picked > 0)
                {
                    IUserMessage msg;
                    if (picked == 1)
                    {
                        msg = await Context.Channel.SendMessageAsync($"{Context.User.Username} picked up 1 {Roki.Properties.CurrencyName}.").ConfigureAwait(false);
                    }
                    else
                    {
                        msg = await Context.Channel.SendMessageAsync($"{Context.User.Username} picked up {picked:N0} {Roki.Properties.CurrencyNamePlural}.").ConfigureAwait(false);
                    }

                    msg.DeleteAfter(10);
                }
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Drop([Leftover] long amount = 1)
            {
                Context.Message.DeleteAfter(10);
                if (amount < 0)
                {
                    return;
                }

                bool success = await Service.DropAsync(Context, Context.User, amount).ConfigureAwait(false);

                if (!success)
                {
                    await Context.Channel.SendMessageAsync($"You do not have enough {Roki.Properties.CurrencyIcon} to drop.").ConfigureAwait(false);
                }
            }
        }
    }
}