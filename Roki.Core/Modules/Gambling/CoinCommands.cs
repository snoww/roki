using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database;
using Roki.Services.Database.Models;

namespace Roki.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        [RequireContext(ContextType.Guild)]
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
            private readonly IConfigurationService _config;
            private readonly Random _rng = new();

            public CoinCommands(ICurrencyService currency, IConfigurationService config)
            {
                _currency = currency;
                _config = config;
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task BetFlip(long amount, BetFlipGuess guess)
            {
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);
                if (amount < guildConfig.BetFlipMin)
                {
                    await Context.Channel.SendErrorAsync($"The minimum bet is `{guildConfig.BetFlipMin}` {guildConfig.CurrencyIcon}")
                        .ConfigureAwait(false);
                    return;
                }
                
                bool removed = await _currency.RemoveCurrencyAsync(Context.User.Id, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, "BetFlip Entry", amount).ConfigureAwait(false);

                if (!removed)
                {
                    await Context.Channel.SendErrorAsync($"Not enough {guildConfig.CurrencyIcon}\n" +
                                                         $"You have `{await _currency.GetCurrencyAsync(Context.User.Id, Context.Guild.Id):N0}`")
                        .ConfigureAwait(false);
                    return;
                }

                // TODO get images
                BetFlipGuess result = _rng.Next(0, 2) == 1 ? BetFlipGuess.Heads : BetFlipGuess.Tails;

                if (guess == result)
                {
                    var payout = (long) Math.Ceiling(amount * guildConfig.BetFlipMultiplier);
                    await _currency.AddCurrencyAsync(Context.User.Id, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, "BetFlip Payout", payout).ConfigureAwait(false);
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription(
                                $"Result is: {result}\n{Context.User.Mention} Congratulations! You've won `{payout:N0}` {guildConfig.CurrencyIcon}\n" +
                                $"New Balance: `{await _currency.GetCurrencyAsync(Context.User.Id, Context.Guild.Id):N0}` {guildConfig.CurrencyIcon}"))
                        .ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                            .WithDescription($"Result is: {result}\n{Context.User.Mention} Better luck next time!\n" +
                                             $"New Balance: `{await _currency.GetCurrencyAsync(Context.User.Id, Context.Guild.Id):N0}` {guildConfig.CurrencyIcon}"))
                        .ConfigureAwait(false);
                }
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task BetFlipMulti(long amount, params BetFlipGuess[] guesses)
            {
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

                if (guesses.Length < guildConfig.BetFlipMMinGuesses)
                {
                    await Context.Channel.SendErrorAsync($"Needs at least `{guildConfig.BetFlipMMinGuesses}` guesses.").ConfigureAwait(false);
                    return;
                }

                int minAmount = (int) Math.Floor(guesses.Length * guildConfig.BetFlipMMinMultiplier);
                if (guesses.Length >= guildConfig.BetFlipMMinGuesses && amount < minAmount)
                {
                    await Context.Channel.SendErrorAsync($"`{guesses.Length}` guesses requires you to bet at least `{minAmount:N0}` {guildConfig.CurrencyIcon}.")
                        .ConfigureAwait(false);
                    return;
                }
                
                bool removed = await _currency.RemoveCurrencyAsync(Context.User.Id, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, "BetFlipMulti Entry", amount).ConfigureAwait(false);

                if (!removed)
                {
                    await Context.Channel.SendErrorAsync($"Not enough {guildConfig.CurrencyIcon}\n" +
                                                         $"You have `{await _currency.GetCurrencyAsync(Context.User.Id, Context.Guild.Id):N0}`")
                        .ConfigureAwait(false);
                    return;
                }

                var results = new List<BetFlipGuess>(guesses.Length);
                for (var i = 0; i < guesses.Length; i++) results.Add(_rng.Next(0, 2) == 1 ? BetFlipGuess.Heads : BetFlipGuess.Tails);

                int correct = guesses.Where((t, i) => t == results[i]).Count();
                double percentage = (double) correct / guesses.Length;

                if (percentage >= guildConfig.BetFlipMMinCorrect)
                {
                    var payout = (long) Math.Ceiling(amount * Math.Pow(correct, guildConfig.BetFlipMMultiplier));
                    await _currency.AddCurrencyAsync(Context.User.Id, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, "BetFlipMulti Payout", payout).ConfigureAwait(false);
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                            .WithDescription(
                                $"Results are: {string.Join(", ", results)}\n{Context.User.Mention} Congratulations! You got `{correct}/{guesses.Length} ({percentage:P0})` correct. You've won `{payout:N0}` {guildConfig.CurrencyIcon}" +
                                $"New Balance: `{await _currency.GetCurrencyAsync(Context.User.Id, Context.Guild.Id):N0}` {guildConfig.CurrencyIcon}"))
                        .ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                            .WithDescription($"Results are: {string.Join(", ", results)}\n{Context.User.Mention} You got `{correct}/{guesses.Length} ({percentage:P2})` correct. You need at least `{guildConfig.BetFlipMMinCorrect:P0}`. Better luck next time!\n" +
                                             $"New Balance: `{await _currency.GetCurrencyAsync(Context.User.Id, Context.Guild.Id):N0}` {guildConfig.CurrencyIcon}"))
                        .ConfigureAwait(false);    
                }
            }
        }
    }
}