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
            public enum BetFlipGuess
            {
                Heads = 0,
                Tails = 1,
                H = 0,
                T = 1,
                Head = 0,
                Tail = 1
            }

            private readonly ICurrencyService _currency;
            private readonly Random _rng = new();

            public CoinCommands(ICurrencyService currency)
            {
                _currency = currency;
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task BetFlip(long amount, BetFlipGuess guess)
            {
                // TODO min/max bet amounts
                if (amount < Roki.Properties.BetFlipMin)
                {
                    await Context.Channel.SendErrorAsync($"The minimum bet is `{Roki.Properties.BetFlipMin}` {Roki.Properties.CurrencyIcon}")
                        .ConfigureAwait(false);
                    return;
                }

                bool removed = await _currency
                    .RemoveAsync(Context.User.Id, "BetFlip Entry", amount, Context.Guild.Id, Context.Channel.Id, Context.Message.Id)
                    .ConfigureAwait(false);

                if (!removed)
                {
                    await Context.Channel.SendErrorAsync($"Not enough {Roki.Properties.CurrencyIcon}\n" +
                                                         $"You have `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}`")
                        .ConfigureAwait(false);
                    return;
                }

                // TODO get images
                BetFlipGuess result = _rng.Next(0, 2) == 1 ? BetFlipGuess.Heads : BetFlipGuess.Tails;

                if (guess == result)
                {
                    var won = (long) Math.Ceiling(amount * Roki.Properties.BetFlipMultiplier);
                    await _currency.AddAsync(Context.User.Id, "BetFlip Payout", won, Context.Guild.Id, Context.Channel.Id, Context.Message.Id).ConfigureAwait(false);
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription(
                                $"Result is: {result}\n{Context.User.Mention} Congratulations! You've won `{won:N0}` {Roki.Properties.CurrencyIcon}\n" +
                                $"New Balance: `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}` {Roki.Properties.CurrencyIcon}"))
                        .ConfigureAwait(false);
                    return;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                        .WithDescription($"Result is: {result}\n{Context.User.Mention} Better luck next time!\n" +
                                         $"New Balance: `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}` {Roki.Properties.CurrencyIcon}"))
                    .ConfigureAwait(false);
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task BetFlipMulti(long amount, params BetFlipGuess[] guesses)
            {
                if (amount <= 0)
                {
                    return;
                }

                if (guesses.Length < Roki.Properties.BetFlipMMinGuesses)
                {
                    await Context.Channel.SendErrorAsync("Needs at least `5` guesses.").ConfigureAwait(false);
                    return;
                }

                int minAmount = guesses.Length * 2;
                if (guesses.Length >= Roki.Properties.BetFlipMMinGuesses && amount < minAmount)
                {
                    await Context.Channel.SendErrorAsync($"`{guesses.Length}` guesses requires you to bet at least `{minAmount:N0}` {Roki.Properties.CurrencyIcon}.")
                        .ConfigureAwait(false);
                    return;
                }

                bool removed = await _currency
                    .RemoveAsync(Context.User.Id, "BetFlipMulti Entry", amount, Context.Guild.Id, Context.Channel.Id, Context.Message.Id)
                    .ConfigureAwait(false);

                if (!removed)
                {
                    await Context.Channel.SendErrorAsync($"Not enough {Roki.Properties.CurrencyIcon}\n" +
                                                         $"You have `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}`")
                        .ConfigureAwait(false);
                    return;
                }

                var results = new List<BetFlipGuess>();
                for (var i = 0; i < guesses.Length; i++) results.Add(_rng.Next(0, 2) == 1 ? BetFlipGuess.Heads : BetFlipGuess.Tails);

                int correct = guesses.Where((t, i) => t == results[i]).Count();

                if ((float) correct / guesses.Length >= Roki.Properties.BetFlipMMinCorrect)
                {
                    var won = (long) Math.Ceiling(amount * Math.Pow(correct, Roki.Properties.BetFlipMMultiplier));
                    await _currency.AddAsync(Context.User.Id, "BetFlipMulti Payout", won, Context.Guild.Id, Context.Channel.Id, Context.Message.Id)
                        .ConfigureAwait(false);
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription(
                                $"Results are: {string.Join(", ", results)}\n{Context.User.Mention} Congratulations! You got `{correct}/{guesses.Length}` correct. You've won `{won:N0}` {Roki.Properties.CurrencyIcon}" +
                                $"New Balance: `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}` {Roki.Properties.CurrencyIcon}"))
                        .ConfigureAwait(false);
                    return;
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                        .WithDescription($"Results are: {string.Join(", ", results)}\n{Context.User.Mention} You got `{correct}/{guesses.Length}` correct. Better luck next time!\n" +
                                         $"New Balance: `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}` {Roki.Properties.CurrencyIcon}"))
                    .ConfigureAwait(false);
            }
        }
    }
}