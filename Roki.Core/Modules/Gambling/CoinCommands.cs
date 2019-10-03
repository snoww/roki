using System;
using System.Threading.Tasks;
using Discord;
using LinqToTwitter;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Gambling
{
    public partial class Gambling
    {
        public class CoinCommands : RokiSubmodule
        {
            private readonly DbService _db;
            private readonly ICurrencyService _currency;
            private static readonly Random Rng = new Random();

            public CoinCommands(DbService db, ICurrencyService currency)
            {
                _db = db;
                _currency = currency;
            }
            
            public enum BetFlipGuess
            {
                H = 0,
                Head = 0,
                Heads = 0,
                T = 1,
                Tail = 1,
                Tails = 1
            }
            
            [RokiCommand, Description, Aliases, Usage]
            public async Task BetFlip(long amount, BetFlipGuess guess)
            {
                // TODO min/max bet amounts
                if (amount < 0)
                    return;

                var removed = await _currency
                    .ChangeAsync(ctx.User, "Betflip Entry", -amount, ctx.User.Id.ToString(), "Server", ctx.Guild.Id, ctx.Channel.Id, 
                        ctx.Message.Id)
                    .ConfigureAwait(false);

                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync("Not enough stones.").ConfigureAwait(false);
                    return;
                }

                BetFlipGuess result;
                if (Rng.Next(0, 1) == 1)
                {
                    result = BetFlipGuess.Heads;
                }
                else
                {
                    result = BetFlipGuess.Tails;
                }
                
                if (guess == result)
                {
                    var won = (long) (amount * 1.98);
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithDescription($"{ctx.User.Mention} Congratulations! You've won {won} stones")).ConfigureAwait(false);
                    await _currency.ChangeAsync(ctx.User, "Betflip Award", won, "Server", ctx.User.Id.ToString(), ctx.Guild.Id,
                        ctx.Channel.Id, ctx.Message.Id);
                    return;
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithDescription($"{ctx.User.Mention} Better luck next time!")).ConfigureAwait(false);
            }
        }
    }
    
}