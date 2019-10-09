using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
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
                Heads = 0,
                Tails = 1,
                H = 0,
                T = 1,
                Head = 0,
                Tail = 1,
            }
            
            [RokiCommand, Description, Aliases, Usage]
            public async Task BetFlip(long amount, BetFlipGuess guess)
            {
                // TODO min/max bet amounts
                if (amount <= 0)
                    return;

                var removed = await _currency
                    .ChangeAsync(ctx.User, "BetFlip Entry", -amount, ctx.User.Id.ToString(), "Server", ctx.Guild.Id, ctx.Channel.Id, 
                        ctx.Message.Id)
                    .ConfigureAwait(false);

                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync("Not enough stones.").ConfigureAwait(false);
                    return;
                }

                BetFlipGuess result;
                if (Rng.Next(0, 2) == 1)
                {
                    result = BetFlipGuess.Heads;
                }
                else
                {
                    result = BetFlipGuess.Tails;
                }
                
                if (guess == result)
                {
                    var won = (long) Math.Ceiling(amount * 1.95);
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithDescription($"Result is: {result}\n{ctx.User.Mention} Congratulations! You've won {won} stones")).ConfigureAwait(false);
                    await _currency.ChangeAsync(ctx.User, "BetFlip Payout", won, "Server", ctx.User.Id.ToString(), ctx.Guild.Id,
                        ctx.Channel.Id, ctx.Message.Id);
                    return;
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithDescription($"Result is: {result}\n{ctx.User.Mention} Better luck next time!")).ConfigureAwait(false);
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task BetFlipMulti(long amount, params BetFlipGuess[] guesses)
            {
                if (amount <= 0)
                    return;

                if (guesses.Length < 5)
                {
                    await ctx.Channel.SendErrorAsync("Needs at least 5 guesses.").ConfigureAwait(false);
                    return;
                }

                var minAmount = guesses.Length * 2;
                if (guesses.Length >= 5 && amount < minAmount)
                {
                    await ctx.Channel.SendErrorAsync($"{guesses.Length} guesses requires you to bet at least {minAmount} stones.").ConfigureAwait(false);
                    return;
                }

                var removed = await _currency
                    .ChangeAsync(ctx.User, "BetFlipMulti Entry", -amount, ctx.User.Id.ToString(), "Server", ctx.Guild.Id, ctx.Channel.Id, 
                        ctx.Message.Id)
                    .ConfigureAwait(false);

                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync("Not enough stones.").ConfigureAwait(false);
                    return;
                }

                var results = new List<BetFlipGuess>();
                for (int i = 0; i < guesses.Length; i++)
                {
                    results.Add(Rng.Next(0, 2) == 1 ? BetFlipGuess.Heads : BetFlipGuess.Tails);
                }

                var correct = guesses.Where((t, i) => t == results[i]).Count();

                if ((float) correct / guesses.Length >= 0.75)
                {
                    var won = (long) Math.Ceiling(amount * Math.Pow(correct, 1.1));
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithDescription($"Results are: {string.Join(", ", results)}\n{ctx.User.Mention} Congratulations! You got {correct}/{guesses.Length} correct. You've won {won} stones"))
                        .ConfigureAwait(false);
                    await _currency.ChangeAsync(ctx.User, "BetFlipMulti Payout", won, "Server", ctx.User.Id.ToString(), ctx.Guild.Id,
                        ctx.Channel.Id, ctx.Message.Id);
                    return;
                }
                
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithDescription($"Results are: {string.Join(", ", results)}\n{ctx.User.Mention} You got {correct}/{guesses.Length} correct. Better luck next time!"))
                    .ConfigureAwait(false);
            }
        }
    }
}