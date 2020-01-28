using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Stocks.Services;

namespace Roki.Modules.Stocks
{
    public partial class Stocks
    {
        [Group]
        public class PortfolioCommands : RokiSubmodule<PortfolioService>
        {
            [RokiCommand, Usage, Description, Aliases]
            public async Task Portfolio(IUser optionalUser = null)
            {
                var user = optionalUser ?? ctx.User;
                var portfolio = await _service.GetUserPortfolio(user.Id).ConfigureAwait(false);
                if (portfolio == null || portfolio.Count < 1)
                {
                    await ctx.Channel.SendErrorAsync($"{user.Mention} You do not currently have a portfolio. Invest in some companies to create your portfolio.").ConfigureAwait(false);
                    return;
                }

                var value = await _service.GetPortfolioValue(portfolio).ConfigureAwait(false);

                var itemsPP = 10;
                await ctx.SendPaginatedConfirmAsync(0, p =>
                {
                    var startAt = itemsPP * p;
                    var desc = string.Join("\n", portfolio
                        .Skip(startAt)
                        .Take(itemsPP)
                        .Select(i => $"`{i.Symbol.ToUpper()}` `{i.Position}` - `{i.Shares}` shares"));

                    desc = $"Your current portfolio value:\n`{value:N2}` {Roki.Services.Roki.Properties.CurrencyIcon}\n" + desc;
                    
                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle($"{user.Username}'s Portfolio")
                        .WithDescription(desc);

                    return embed;
                }, portfolio.Count, itemsPP, false);
            }
        }
    }
}