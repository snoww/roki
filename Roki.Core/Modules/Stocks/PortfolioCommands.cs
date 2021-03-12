using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Stocks.Services;
using Roki.Services;
using Roki.Services.Database.Models;

namespace Roki.Modules.Stocks
{
    public partial class Stocks
    {
        [Group]
        [RequireContext(ContextType.Guild)]
        public class PortfolioCommands : RokiSubmodule<PortfolioService>
        {
            private readonly IConfigurationService _config;

            public PortfolioCommands(IConfigurationService config)
            {
                _config = config;
            }

            [RokiCommand, Usage, Description, Aliases]
            public async Task Portfolio(IUser optionalUser = null)
            {
                IUser user = optionalUser ?? Context.User;
                List<Investment> portfolio = await Service.GetUserPortfolio(user.Id, Context.Guild.Id).ConfigureAwait(false);
                if (portfolio == null || portfolio.Count < 1)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context).WithTitle($"{Context.User.Username}'s Portfolio")
                        .WithDescription($"{user.Mention} You do not currently have a portfolio. Invest in some companies to create your portfolio.\n" +
                                         $"Check out the stocks commands with `{await _config.GetGuildPrefix(Context.Guild.Id)}commands stocks`"));
                    return;
                }

                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
                decimal value = await Service.GetPortfolioValue(portfolio).ConfigureAwait(false);

                const int itemsPerPage = 10;
                await Context.SendPaginatedMessageAsync(0, p =>
                {
                    string desc = string.Join("\n", portfolio
                        .Skip(itemsPerPage * p)
                        .Take(itemsPerPage)
                        .Select(i => $"`{i.Symbol}` `{(i.Shares > 0 ? "long" : "short")}` - `{i.Shares}` shares"));

                    desc = $"Your current portfolio value:\n`{value:N2}` {guildConfig.CurrencyIcon}\n" + desc;

                    EmbedBuilder embed = new EmbedBuilder().WithDynamicColor(Context)
                        .WithTitle($"{user.Username}'s Portfolio")
                        .WithDescription(desc);

                    return embed;
                }, portfolio.Count, itemsPerPage, false);
            }
        }
    }
}