using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class CoinCommands : RokiSubmodule
        {
            private readonly ICurrencyService _currency;
            private static readonly Random Rng = new Random();

            public CoinCommands(ICurrencyService currency)
            {
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
                if (amount < Services.Roki.Properties.BetFlipMin)
                {
                    await ctx.Channel.SendErrorAsync($"The minimum bet is `{Services.Roki.Properties.BetFlipMin}` {Services.Roki.Properties.CurrencyIcon}")
                        .ConfigureAwait(false);
                    return;
                }

                var removed = await _currency
                    .RemoveAsync(ctx.User.Id, "BetFlip Entry", amount, ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id)
                    .ConfigureAwait(false);

                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync($"Not enough {Services.Roki.Properties.CurrencyIcon}\n" +
                                                     $"You have `{await _currency.GetCurrency(ctx.User.Id, ctx.Guild.Id):N0}`")
                        .ConfigureAwait(false);
                    return;
                }

                BetFlipGuess result;
                // TODO get images
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
                    var won = (long) Math.Ceiling(amount * Services.Roki.Properties.BetFlipMultiplier);
                    await _currency.AddAsync(ctx.User.Id, "BetFlip Payout", won, ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id).ConfigureAwait(false);
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithDescription(
                                $"Result is: {result}\n{ctx.User.Mention} Congratulations! You've won `{won:N0}` {Services.Roki.Properties.CurrencyIcon}\n" +
                                $"New Balance: `{await _currency.GetCurrency(ctx.User.Id, ctx.Guild.Id):N0}` {Services.Roki.Properties.CurrencyIcon}"))
                        .ConfigureAwait(false);
                    return;
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithDescription($"Result is: {result}\n{ctx.User.Mention} Better luck next time!\n" +
                                     $"New Balance: `{await _currency.GetCurrency(ctx.User.Id, ctx.Guild.Id):N0}` {Services.Roki.Properties.CurrencyIcon}"))
                    .ConfigureAwait(false);
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task BetFlipMulti(long amount, params BetFlipGuess[] guesses)
            {
                if (amount <= 0)
                    return;

                if (guesses.Length < Services.Roki.Properties.BetFlipMMinGuesses)
                {
                    await ctx.Channel.SendErrorAsync("Needs at least `5` guesses.").ConfigureAwait(false);
                    return;
                }

                var minAmount = guesses.Length * 2;
                if (guesses.Length >= Services.Roki.Properties.BetFlipMMinGuesses && amount < minAmount)
                {
                    await ctx.Channel.SendErrorAsync($"`{guesses.Length}` guesses requires you to bet at least `{minAmount:N0}` {Services.Roki.Properties.CurrencyIcon}.")
                        .ConfigureAwait(false);
                    return;
                }

                var removed = await _currency
                    .RemoveAsync(ctx.User.Id, "BetFlipMulti Entry", amount, ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id)
                    .ConfigureAwait(false);

                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync($"Not enough {Services.Roki.Properties.CurrencyIcon}\n" +
                                                     $"You have `{await _currency.GetCurrency(ctx.User.Id, ctx.Guild.Id):N0}`")
                        .ConfigureAwait(false);
                    return;
                }

                var results = new List<BetFlipGuess>();
                for (int i = 0; i < guesses.Length; i++)
                {
                    results.Add(Rng.Next(0, 2) == 1 ? BetFlipGuess.Heads : BetFlipGuess.Tails);
                }

                var correct = guesses.Where((t, i) => t == results[i]).Count();

                if ((float) correct / guesses.Length >= Services.Roki.Properties.BetFlipMMinCorrect)
                {
                    var won = (long) Math.Ceiling(amount * Math.Pow(correct, Services.Roki.Properties.BetFlipMMultiplier));
                    await _currency.AddAsync(ctx.User.Id, "BetFlipMulti Payout", won, ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id)
                        .ConfigureAwait(false);
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithDescription(
                                $"Results are: {string.Join(", ", results)}\n{ctx.User.Mention} Congratulations! You got `{correct}/{guesses.Length}` correct. You've won `{won:N0}` {Services.Roki.Properties.CurrencyIcon}" +
                                $"New Balance: `{await _currency.GetCurrency(ctx.User.Id, ctx.Guild.Id):N0}` {Services.Roki.Properties.CurrencyIcon}"))
                        .ConfigureAwait(false);
                    return;
                }
                
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithDescription($"Results are: {string.Join(", ", results)}\n{ctx.User.Mention} You got `{correct}/{guesses.Length}` correct. Better luck next time!\n" +
                                     $"New Balance: `{await _currency.GetCurrency(ctx.User.Id, ctx.Guild.Id):N0}` {Services.Roki.Properties.CurrencyIcon}"))
                    .ConfigureAwait(false);
            }
        }
    }
}