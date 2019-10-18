using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class LotteryCommands : RokiSubmodule
        {
            private readonly ICurrencyService _currency;
            private const string Stone = "<:stone:269130892100763649>";

            public LotteryCommands(ICurrencyService currency)
            {
                _currency = currency;
            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task Jackpot()
            {
                var rokiCurr = _currency.GetCurrency(ctx.Client.CurrentUser.Id);
                if (rokiCurr < 1000)
                    await ctx.Channel.SendErrorAsync("The lottery is currently down. Please check back another time.").ConfigureAwait(false);

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("Stone Lottery")
                        .WithDescription($"Current Jackpot: {Format.Bold(rokiCurr.ToString())} {Stone}"))
                    .ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task JoinLottery()
            {
                var removed = await _currency.ChangeAsync(ctx.User, "Lottery Entry", -10, ctx.User.Id.ToString(), $"{ctx.Client.CurrentUser.Id}",
                    ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id).ConfigureAwait(false);
                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync($"{ctx.User.Mention} you do not have enough currency to join the lottery.")
                        .ConfigureAwait(false);
                    return;
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithDescription($"{ctx.User.Mention} you've joined the lottery."))
                    .ConfigureAwait(false);
            }
        }
    }
}