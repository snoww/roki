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
                if (rokiCurr <= 0)
                {
                    await ctx.Channel.SendErrorAsync("The lottery is currently down. Please check back another time.").ConfigureAwait(false);
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("Stone Lottery")
                        .WithDescription($"Current Jackpot: {Format.Bold(rokiCurr.ToString())} {Stone}"))
                    .ConfigureAwait(false);
            }
        }
    }
}