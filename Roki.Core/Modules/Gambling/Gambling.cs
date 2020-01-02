using System;
using System.Threading.Tasks;
using Discord;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Modules.Gambling.Services;
using Roki.Services;

namespace Roki.Modules.Gambling
{
    public partial class Gambling : RokiTopLevelModule<GamblingService>
    {
        private readonly DbService _db;
        private readonly ICurrencyService _currency;
        private readonly Roki _roki;

        public Gambling(DbService db, ICurrencyService currency, Roki roki)
        {
            _db = db;
            _currency = currency;
            _roki = roki;
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task BetRoll(long amount)
        {
            if (amount < _roki.Properties.BetRollMin)
                return;
            
            var removed = await _currency
                .ChangeAsync(ctx.User.Id, ctx.Client.CurrentUser.Id, "BetRoll Entry", -amount, ctx.Guild.Id, ctx.Channel.Id, 
                    ctx.Message.Id)
                .ConfigureAwait(false);
            if (!removed)
            {
                await ctx.Channel.SendErrorAsync($"Not enough {_roki.Properties.CurrencyIcon}" +
                                                 $"You have `{_service.GetCurrency(ctx.User.Id):N0}`")
                    .ConfigureAwait(false);
                return;
            }
            var roll = new Random().Next(1, 101);
            var rollStr = $"{ctx.User.Mention} rolled `{roll}`.";
            if (roll < 70)
            {
                await ctx.Channel.SendErrorAsync($"{rollStr}\nBetter luck next time." +
                                                 $"New Balance: `{_service.GetCurrency(ctx.User.Id):N0}` {_roki.Properties.CurrencyIcon}")
                    .ConfigureAwait(false);
                return;
            }
            
            long win;
            if (roll < 91)
                win = (long) Math.Ceiling(amount * _roki.Properties.BetRoll71Multiplier);
            else if (roll < 100)
                win = amount * _roki.Properties.BetRoll92Multiplier;
            else
                win = amount * _roki.Properties.BetRoll100Multiplier;

            await _currency.ChangeAsync(ctx.Client.CurrentUser.Id, ctx.User.Id, "BetRoll Payout", win, ctx.Guild.Id, ctx.Channel.Id,
                ctx.Message.Id);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription($"{rollStr}\nCongratulations, you won `{win:N0}` {_roki.Properties.CurrencyIcon}" +
                                 $"New Balance: `{_service.GetCurrency(ctx.User.Id):N0}` {_roki.Properties.CurrencyIcon}"));
        }
    }
}