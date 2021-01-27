using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Gambling
{
    [RequireContext(ContextType.Guild)]
    public partial class Gambling : RokiTopLevelModule
    {
        private readonly ICurrencyService _currency;
        private readonly IConfigurationService _config;
        private readonly Random _rng = new();

        public Gambling(ICurrencyService currency, IConfigurationService config)
        {
            _currency = currency;
            _config = config;
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task BetRoll(long amount)
        {
            GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

            if (amount < guildConfig.BetRollMin)
            {
                return;
            }

            bool removed = await _currency
                .RemoveAsync(Context.User, Context.Client.CurrentUser, "BetRoll Entry", amount, Context.Guild.Id, Context.Channel.Id, Context.Message.Id)
                .ConfigureAwait(false);
            if (!removed)
            {
                await Context.Channel.SendErrorAsync($"Not enough {guildConfig.CurrencyIcon}\n" +
                                                     $"You have `{await _currency.GetCurrency(Context.User, Context.Guild.Id):N0}`")
                    .ConfigureAwait(false);
                return;
            }

            int roll = _rng.Next(1, 101);
            var rollStr = $"{Context.User.Mention} rolled `{roll}`.";
            if (roll < 70)
            {
                await Context.Channel.SendErrorAsync($"{rollStr}\nBetter luck next time.\n" +
                                                     $"New Balance: `{await _currency.GetCurrency(Context.User, Context.Guild.Id):N0}` {guildConfig.CurrencyIcon}")
                    .ConfigureAwait(false);
                return;
            }

            long win;
            if (roll < 91)
            {
                win = (long) Math.Ceiling(amount * guildConfig.BetRoll71Multiplier);
            }
            else if (roll < 100)
            {
                win = amount * guildConfig.BetRoll92Multiplier;
            }
            else
            {
                win = amount * guildConfig.BetRoll100Multiplier;
            }

            await _currency.AddAsync(Context.User, Context.Client.CurrentUser, "BetRoll Payout", win, Context.Guild.Id, Context.Channel.Id, Context.Message.Id).ConfigureAwait(false);
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithDescription($"{rollStr}\nCongratulations, you won `{win:N0}` {guildConfig.CurrencyIcon}\n" +
                                     $"New Balance: `{await _currency.GetCurrency(Context.User, Context.Guild.Id):N0}` {guildConfig.CurrencyIcon}"))
                .ConfigureAwait(false);
        }
    }
}