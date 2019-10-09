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
            if (amount <= 0)
                return;
            
            var removed = await _currency
                .ChangeAsync(ctx.User, "BetRoll Entry", -amount, ctx.User.Id.ToString(), "Server", ctx.Guild.Id, ctx.Channel.Id, 
                    ctx.Message.Id)
                .ConfigureAwait(false);
            if (!removed)
            {
                await ctx.Channel.SendErrorAsync("Not enough stones.").ConfigureAwait(false);
                return;
            }
            var roll = new Random().Next(1, 101);
            var rollStr = $"{ctx.User.Mention} rolled {roll}.";
            if (roll < 70)
            {
                await ctx.Channel.SendErrorAsync($"{rollStr}\nBetter luck next time.");
                return;
            }
            
            long win;
            if (roll < 91)
            {
                win = (long) Math.Ceiling(amount * 2.5);
            }
            else if (roll < 100)
            {
                win = amount * 4;
            }
            else
            {
                win = amount * 10;
            }
            
            await _currency.ChangeAsync(ctx.User, "BetRoll Payout", win, "Server", ctx.User.Id.ToString(), ctx.Guild.Id, ctx.Channel.Id,
                ctx.Message.Id);
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription($"{rollStr}\nCongratulations, you won {win} stones."));
        }
    }
}