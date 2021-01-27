using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Stocks.Services;
using Roki.Services;
using Roki.Services.Database.Maps;

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
                Dictionary<string, Investment> portfolio = await Service.GetUserPortfolio(user, Context.Guild.Id).ConfigureAwait(false);
                if (portfolio == null || portfolio.Count < 1)
                {
                    await Context.Channel.SendErrorAsync($"{user.Mention} You do not currently have a portfolio. Invest in some companies to create your portfolio.").ConfigureAwait(false);
                    return;
                }

                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
                decimal value = await Service.GetPortfolioValue(portfolio).ConfigureAwait(false);

                const int itemsPerPage = 10;
                await Context.SendPaginatedMessageAsync(0, p =>
                {
                    int startAt = itemsPerPage * p;
                    string desc = string.Join("\n", portfolio
                        .Skip(startAt)
                        .Take(itemsPerPage)
                        .Select(i => $"`{i.Key.ToUpper()}` `{i.Value.Position}` - `{i.Value.Shares}` shares"));

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