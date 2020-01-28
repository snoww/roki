using System;
using System.Threading.Tasks;
using Discord;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Gambling
{
    public partial class Gambling : RokiTopLevelModule
    {
        private readonly DbService _db;
        private readonly ICurrencyService _currency;

        public Gambling(DbService db, ICurrencyService currency)
        {
            _db = db;
            _currency = currency;
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task BetRoll(long amount)
        {
            if (amount < Roki.Properties.BetRollMin)
                return;
            
            var removed = await _currency
                .RemoveAsync(ctx.User.Id,"BetRoll Entry", amount, ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id)
                .ConfigureAwait(false);
            if (!removed)
            {
                await ctx.Channel.SendErrorAsync($"Not enough {Roki.Properties.CurrencyIcon}\n" +
                                                 $"You have `{await _currency.GetCurrency(ctx.User.Id, ctx.Guild.Id):N0}`")
                    .ConfigureAwait(false);
                return;
            }
            var roll = new Random().Next(1, 101);
            var rollStr = $"{ctx.User.Mention} rolled `{roll}`.";
            if (roll < 70)
            {
                await ctx.Channel.SendErrorAsync($"{rollStr}\nBetter luck next time.\n" +
                                                 $"New Balance: `{await _currency.GetCurrency(ctx.User.Id, ctx.Guild.Id):N0}` {Roki.Properties.CurrencyIcon}")
                    .ConfigureAwait(false);
                return;
            }
            
            long win;
            if (roll < 91)
                win = (long) Math.Ceiling(amount * Roki.Properties.BetRoll71Multiplier);
            else if (roll < 100)
                win = amount * Roki.Properties.BetRoll92Multiplier;
            else
                win = amount * Roki.Properties.BetRoll100Multiplier;

            await _currency.AddAsync(ctx.User.Id, "BetRoll Payout", win, ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithDescription($"{rollStr}\nCongratulations, you won `{win:N0}` {Roki.Properties.CurrencyIcon}\n" +
                                     $"New Balance: `{await _currency.GetCurrency(ctx.User.Id, ctx.Guild.Id):N0}` {Roki.Properties.CurrencyIcon}"))
                .ConfigureAwait(false);
        }
    }
}