using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Currency.Services;
using Roki.Services;

namespace Roki.Modules.Currency
{
    public partial class Currency
    {
        [Group]
        [RequireContext(ContextType.Guild)]
        public class PickDropCommands : RokiSubmodule<PickDropService>
        {
            private readonly IConfigurationService _config;

            public PickDropCommands(IConfigurationService config)
            {
                _config = config;
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Pick()
            {
                await Context.Message.DeleteAsync().ConfigureAwait(false);
                long picked = await Service.PickAsync((ITextChannel) Context.Channel, Context.User.Id, Context.Message.Id).ConfigureAwait(false);

                if (picked > 0)
                {
                    IUserMessage msg;
                    if (picked == 1)
                    {
                        msg = await Context.Channel.SendMessageAsync($"{Context.User.Username} picked up 1 {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyName}.").ConfigureAwait(false);
                    }
                    else
                    {
                        msg = await Context.Channel.SendMessageAsync($"{Context.User.Username} picked up {picked:N0} {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyNamePlural}.").ConfigureAwait(false);
                    }

                    msg.DeleteAfter(10);
                }
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Drop([Leftover] long amount = 1)
            {
                Context.Message.DeleteAfter(10);
                if (amount < 0)
                {
                    return;
                }

                bool success = await Service.DropAsync(Context, amount).ConfigureAwait(false);

                if (!success)
                {
                    await Context.Channel.SendMessageAsync($"You do not have enough {(await _config.GetGuildConfigAsync(Context.Guild.Id)).CurrencyIcon} to drop.").ConfigureAwait(false);
                }
            }
        }
    }
}