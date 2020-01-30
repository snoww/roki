using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Roki.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class DiceCommands : RokiSubmodule
        {
            private const string Path = "./data/dice/";
            private readonly ICurrencyService _currency;

            public DiceCommands(ICurrencyService currency)
            {
                _currency = currency;
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task BetDie(long amount)
            {
                if (amount < Roki.Properties.BetDieMin)
                {
                    await Context.Channel
                        .SendErrorAsync($"The minimum bet for this game is `{Roki.Properties.BetDieMin:N0}` {Roki.Properties.CurrencyIcon}")
                        .ConfigureAwait(false);
                    return;
                }


                var removed = await _currency
                    .RemoveAsync(Context.User.Id, "BetDie Entry", amount, Context.Guild.Id, Context.Channel.Id, Context.Message.Id)
                    .ConfigureAwait(false);
                if (!removed)
                {
                    await Context.Channel.SendErrorAsync($"Not enough {Roki.Properties.CurrencyIcon}\n" +
                                                     $"You have `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}`")
                        .ConfigureAwait(false);
                    return;
                }

                var rolls = 0;
                var total = 0;
                var die = new List<Image<Rgba32>>(6);
                while (rolls < 6)
                {
                    var rng = new Random().Next(1, 7);
                    total += rng;
                    die.Add(GetDice(rng));
                    rolls += 1;
                }

                long won;
                if (total >= 18 && total <= 24)
                    won = 0;
                else
                    switch (total)
                    {
                        case 17:
                        case 25:
                            won = (long) Math.Ceiling(amount * 1.25);
                            break;
                        case 16:
                        case 26:
                            won = (long) Math.Ceiling(amount * 1.5);
                            break;
                        case 15:
                        case 27:
                            won = amount * 2;
                            break;
                        case 14:
                        case 28:
                            won = (long) Math.Ceiling(amount * 2.5);
                            break;
                        case 13:
                        case 29:
                            won = amount * 3;
                            break;
                        case 12:
                        case 30:
                            won = amount * 5;
                            break;
                        case 11:
                        case 31:
                            won = amount * 7;
                            break;
                        case 10:
                        case 32:
                            won = amount * 10;
                            break;
                        case 9:
                        case 33:
                            won = amount * 17;
                            break;
                        case 8:
                        case 34:
                            won = amount * 25;
                            break;
                        case 7:
                        case 35:
                            won = amount * 50;
                            break;
                        default:
                            won = amount * 100;
                            break;
                    }

                using var image = die.Merge();
                await using var stream = image.ToStream();
                foreach (var dice in die) dice.Dispose();

                var embed = new EmbedBuilder().WithImageUrl("attachment://dice.png");

                if (won > 0)
                {
                    await _currency.AddAsync(Context.User.Id, "BetDie Payout", won, Context.Guild.Id, Context.Channel.Id, Context.Message.Id);
                    embed.WithOkColor()
                        .WithDescription(
                            $"{Context.User.Mention} You rolled a total of `{total}`\nCongratulations! You've won `{won:N0}` {Roki.Properties.CurrencyIcon}\n" +
                            $"New Balance: `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}` {Roki.Properties.CurrencyIcon}");
                }
                else
                {
                    embed.WithErrorColor()
                        .WithDescription($"{Context.User.Mention} You rolled a total of `{total}`\nBetter luck next time!\n" +
                                         $"New Balance: `{await _currency.GetCurrency(Context.User.Id, Context.Guild.Id):N0}` {Roki.Properties.CurrencyIcon}");
                }

                await Context.Channel.SendFileAsync(stream, "dice.png", embed: embed.Build()).ConfigureAwait(false);
            }

            private Image<Rgba32> GetDice(int num)
            {
                if (num < 0 || num > 6)
                    throw new ArgumentOutOfRangeException(nameof(num));
                
                return Image.Load<Rgba32>(Path + $"{num}.png");
            }
        }
    }
}