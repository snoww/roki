using System;
using System.Threading.Tasks;
using Discord;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Gambling
{
    public partial class Gambling : RokiTopLevelModule
    {
        private readonly ICurrencyService _currency;
        private readonly Random _rng = new Random();

        public Gambling(ICurrencyService currency)
        {
            _currency = currency;
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task BetRoll(long amount)
        {
            if (amount < Roki.Properties.BetRollMin)
            {
                return;
            }

            bool removed = await _currency
                .RemoveAsync(Context.User.Id, "BetRoll Entry", amount, Context.Guild.Id, Context.Channel.Id, Context.Message.Id)
                .ConfigureAwait(false);
            if (!removed)
            {
                await Context.Channel.SendErrorAsync($"Not enough {Roki.Properties.CurrencyIcon}\n" +
                                                     $"You have `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}`")
                    .ConfigureAwait(false);
                return;
            }

            int roll = _rng.Next(1, 101);
            var rollStr = $"{Context.User.Mention} rolled `{roll}`.";
            if (roll < 70)
            {
                await Context.Channel.SendErrorAsync($"{rollStr}\nBetter luck next time.\n" +
                                                     $"New Balance: `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}` {Roki.Properties.CurrencyIcon}")
                    .ConfigureAwait(false);
                return;
            }

            long win;
            if (roll < 91)
            {
                win = (long) Math.Ceiling(amount * Roki.Properties.BetRoll71Multiplier);
            }
            else if (roll < 100)
            {
                win = amount * Roki.Properties.BetRoll92Multiplier;
            }
            else
            {
                win = amount * Roki.Properties.BetRoll100Multiplier;
            }

            await _currency.AddAsync(Context.User.Id, "BetRoll Payout", win, Context.Guild.Id, Context.Channel.Id, Context.Message.Id).ConfigureAwait(false);
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithDescription($"{rollStr}\nCongratulations, you won `{win:N0}` {Roki.Properties.CurrencyIcon}\n" +
                                     $"New Balance: `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}` {Roki.Properties.CurrencyIcon}"))
                .ConfigureAwait(false);
        }
    }
}