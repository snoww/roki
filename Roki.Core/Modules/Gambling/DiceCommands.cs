using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Roki.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class DiceCommands : RokiSubmodule
        {
            private readonly ICurrencyService _currency;
            private const string path = "./data/dice/";
            
            public DiceCommands(ICurrencyService currency)
            {
                _currency = currency;
            }

            [RokiCommand, Description, Aliases, Usage]
            public async Task BetDie(long amount)
            {
                if (amount < 10)
                {
                    await ctx.Channel.SendErrorAsync("The minimum bet for this game is 10 stones").ConfigureAwait(false);
                    return;
                }


                var removed = await _currency
                    .ChangeAsync(ctx.User, "BetDie Entry", -amount, ctx.User.Id.ToString(), "Server", ctx.Guild.Id, ctx.Channel.Id,
                        ctx.Message.Id)
                    .ConfigureAwait(false);
                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync("Not enough stones.").ConfigureAwait(false);
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
                if (total >= 19 && total <= 23)
                    won = 0;
                else switch (total)
                {
                    case 18:
                    case 24:
                        won = (long) Math.Ceiling(amount * 1.6);
                        break;
                    case 17:
                    case 25:
                        won = (long) Math.Ceiling(amount * 1.95);
                        break;
                    case 16:
                    case 26:
                        won = amount * 2;
                        break;
                    case 15:
                    case 27:
                        won = amount * 3;
                        break;
                    case 14:
                    case 28:
                        won = amount * 4;
                        break;
                    case 13:
                    case 29:
                        won = amount * 5;
                        break;
                    case 12:
                    case 30:
                        won = amount * 6;
                        break;
                    case 11:
                    case 31:
                        won = amount * 8;
                        break;
                    case 10:
                    case 32:
                        won = amount * 10;
                        break;
                    case 9:
                    case 33:
                        won = amount * 15;
                        break;
                    case 8:
                    case 34:
                        won = amount * 20;
                        break;
                    case 7:
                    case 35:
                        won = amount * 50;
                        break;
                    default:
                        won = amount * 100;
                        break;
                }

                using (var bitmap = die.Merge(out var format))
                using (var ms = bitmap.ToStream(format))
                {
                    foreach (var dice in die)
                    {
                        dice.Dispose();
                    }

                    var embed = new EmbedBuilder()
                        .WithImageUrl($"attachment://dice.{format.FileExtensions.First()}");

                    if (won > 0)
                    {
                        embed.WithOkColor()
                            .WithDescription($"{ctx.User.Mention} You rolled a total of {total}\nCongratulations! You've won {won} stones");
                        await _currency.ChangeAsync(ctx.User, "BetDie Payout", won, "Server", ctx.User.Id.ToString(), ctx.Guild.Id,
                            ctx.Channel.Id, ctx.Message.Id);
                    }
                    else
                    {
                        embed.WithErrorColor()
                            .WithDescription($"{ctx.User.Mention} You rolled a total of {total}\nBetter luck next time!");
                    }

                    await ctx.Channel.SendFileAsync(ms, $"dice.{format.FileExtensions.First()}", embed: embed.Build()).ConfigureAwait(false);
                }
            }

            private Image<Rgba32> GetDice(int num)
            {
                if (num < 0 || num > 6)
                    throw new ArgumentOutOfRangeException(nameof(num));

                var image = Image.Load(path + $"{num}.png");
                using (var ms = new MemoryStream())
                {
                    image.Save(ms, PngFormat.Instance);
                    return Image.Load(ms.ToArray());
                }
            }
        }
    }
}