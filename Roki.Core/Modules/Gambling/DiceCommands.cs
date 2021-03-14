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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Roki.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        [RequireContext(ContextType.Guild)]
        public class DiceCommands : RokiSubmodule
        {
            private const string DicePath = "data/dice/";
            private readonly ICurrencyService _currency;
            private readonly IConfigurationService _config;
            private readonly Random _rng = new();

            public DiceCommands(ICurrencyService currency, IConfigurationService config)
            {
                _currency = currency;
                _config = config;
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task BetDice(long amount)
            {
                GuildConfig guildConfig = await _config.GetGuildConfigAsync(Context.Guild.Id);

                if (amount < guildConfig.BetDiceMin)
                {
                    await Context.Channel
                        .SendErrorAsync($"The minimum bet for this game is `{guildConfig.BetDiceMin:N0}` {guildConfig.CurrencyIcon}")
                        .ConfigureAwait(false);
                    return;
                }

                bool removed = await _currency.RemoveCurrencyAsync(Context.User.Id, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, "BetDice Entry", amount).ConfigureAwait(false);
                if (!removed)
                {
                    await Context.Channel.SendErrorAsync($"Not enough {guildConfig.CurrencyIcon}\n" +
                                                         $"You have `{await _currency.GetCurrencyAsync(Context.User.Id, Context.Guild.Id):N0}`")
                        .ConfigureAwait(false);
                    return;
                }

                var rolls = 0;
                var total = 0;
                var die = new List<Image<Rgba32>>(6);
                while (rolls < 6)
                {
                    int rng = _rng.Next(1, 7);
                    total += rng;
                    die.Add(GetDice(rng));
                    rolls += 1;
                }

                long payout;
                if (total >= 18 && total <= 24)
                {
                    payout = 0;
                }
                else
                {
                    switch (total)
                    {
                        case 17:
                        case 25:
                            payout = (long) Math.Ceiling(amount * 1.25);
                            break;
                        case 16:
                        case 26:
                            payout = (long) Math.Ceiling(amount * 1.5);
                            break;
                        case 15:
                        case 27:
                            payout = amount * 2;
                            break;
                        case 14:
                        case 28:
                            payout = (long) Math.Ceiling(amount * 2.5);
                            break;
                        case 13:
                        case 29:
                            payout = amount * 3;
                            break;
                        case 12:
                        case 30:
                            payout = amount * 5;
                            break;
                        case 11:
                        case 31:
                            payout = amount * 7;
                            break;
                        case 10:
                        case 32:
                            payout = amount * 10;
                            break;
                        case 9:
                        case 33:
                            payout = amount * 17;
                            break;
                        case 8:
                        case 34:
                            payout = amount * 25;
                            break;
                        case 7:
                        case 35:
                            payout = amount * 50;
                            break;
                        default:
                            payout = amount * 100;
                            break;
                    }
                }

                using Image<Rgba32> image = die.Merge();
                await using var stream = image.ToStream();
                foreach (Image<Rgba32> dice in die) dice.Dispose();

                EmbedBuilder embed = new EmbedBuilder().WithImageUrl("attachment://dice.png");

                if (payout > 0)
                {
                    await _currency.AddCurrencyAsync(Context.User.Id, Context.Guild.Id, Context.Channel.Id, Context.Message.Id, "BetDice Payout", payout).ConfigureAwait(false);
                    embed.WithDynamicColor(Context)
                        .WithDescription(
                            $"{Context.User.Mention} You rolled a total of `{total}`\nCongratulations! You've won `{payout:N0}` {guildConfig.CurrencyIcon}\n" +
                            $"New Balance: `{await _currency.GetCurrencyAsync(Context.User.Id, Context.Guild.Id):N0}` {guildConfig.CurrencyIcon}");
                }
                else
                {
                    embed.WithErrorColor()
                        .WithDescription($"{Context.User.Mention} You rolled a total of `{total}`\nBetter luck next time!\n" +
                                         $"New Balance: `{await _currency.GetCurrencyAsync(Context.User.Id, Context.Guild.Id):N0}` {guildConfig.CurrencyIcon}");
                }

                await Context.Channel.SendFileAsync(stream, "dice.png", embed: embed.Build()).ConfigureAwait(false);
            }

            private Image<Rgba32> GetDice(int num)
            {
                if (num < 0 || num > 6)
                {
                    throw new ArgumentOutOfRangeException(nameof(num));
                }

                return Image.Load<Rgba32>(DicePath + $"{num}.png");
            }
        }
    }
}