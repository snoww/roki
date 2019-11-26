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
            private readonly Roki _roki;

            public CoinCommands(DbService db, ICurrencyService currency, Roki roki)
            {
                _db = db;
                _currency = currency;
                _roki = roki;
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
                if (amount < _roki.Properties.BetFlipMin)
                {
                    await ctx.Channel.SendErrorAsync($"The minimum bet is `{_roki.Properties.BetFlipMin}` {_roki.Properties.CurrencyIcon}")
                        .ConfigureAwait(false);
                    return;
                }

                var removed = await _currency
                    .ChangeAsync(ctx.User.Id, "BetFlip Entry", -amount, ctx.User.Id, ctx.Client.CurrentUser.Id, ctx.Guild.Id, ctx.Channel.Id, 
                        ctx.Message.Id)
                    .ConfigureAwait(false);

                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync($"Not enough {_roki.Properties.CurrencyIcon}").ConfigureAwait(false);
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
                    var won = (long) Math.Ceiling(amount * _roki.Properties.BetFlipMultiplier);
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithDescription(
                                $"Result is: {result}\n{ctx.User.Mention} Congratulations! You've won `{won:N0}` {_roki.Properties.CurrencyNamePlural}"))
                        .ConfigureAwait(false);
                    await _currency.ChangeAsync(ctx.User.Id, "BetFlip Payout", won, ctx.Client.CurrentUser.Id, ctx.User.Id, ctx.Guild.Id,
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

                if (guesses.Length < _roki.Properties.BetFlipMMinGuesses)
                {
                    await ctx.Channel.SendErrorAsync("Needs at least `5` guesses.").ConfigureAwait(false);
                    return;
                }

                var minAmount = guesses.Length * 2;
                if (guesses.Length >= _roki.Properties.BetFlipMMinGuesses && amount < minAmount)
                {
                    await ctx.Channel.SendErrorAsync($"`{guesses.Length}` guesses requires you to bet at least `{minAmount:N0}` {_roki.Properties.CurrencyNamePlural}.")
                        .ConfigureAwait(false);
                    return;
                }

                var removed = await _currency
                    .ChangeAsync(ctx.User.Id, "BetFlipMulti Entry", -amount, ctx.User.Id, ctx.Client.CurrentUser.Id, ctx.Guild.Id, ctx.Channel.Id, 
                        ctx.Message.Id)
                    .ConfigureAwait(false);

                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync($"Not enough {_roki.Properties.CurrencyIcon}").ConfigureAwait(false);
                    return;
                }

                var results = new List<BetFlipGuess>();
                for (int i = 0; i < guesses.Length; i++)
                {
                    results.Add(Rng.Next(0, 2) == 1 ? BetFlipGuess.Heads : BetFlipGuess.Tails);
                }

                var correct = guesses.Where((t, i) => t == results[i]).Count();

                if ((float) correct / guesses.Length >= _roki.Properties.BetFlipMMinCorrect)
                {
                    var won = (long) Math.Ceiling(amount * Math.Pow(correct, _roki.Properties.BetFlipMMultiplier));
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithDescription(
                                $"Results are: {string.Join(", ", results)}\n{ctx.User.Mention} Congratulations! You got `{correct}/{guesses.Length}` correct. You've won `{won:N0}` {_roki.Properties.CurrencyNamePlural}"))
                        .ConfigureAwait(false);
                    await _currency.ChangeAsync(ctx.User.Id, "BetFlipMulti Payout", won, ctx.Client.CurrentUser.Id, ctx.User.Id, ctx.Guild.Id,
                        ctx.Channel.Id, ctx.Message.Id);
                    return;
                }
                
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithDescription($"Results are: {string.Join(", ", results)}\n{ctx.User.Mention} You got `{correct}/{guesses.Length}` correct. Better luck next time!"))
                    .ConfigureAwait(false);
            }
        }
    }
}