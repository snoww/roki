using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
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
                else if (total == 18 || total == 24)
                    won = (long) Math.Ceiling(amount * 1.95);
                else if (total == 17 || total == 25)
                    won = amount * 2;
                else if (total == 16 || total == 26)
                    won = (long) Math.Ceiling(amount * 2.3);
                else if (total == 15 || total == 27)
                    won = (long) Math.Ceiling(amount * 2.8);
                else if (total == 14 || total == 28)
                    won = amount * 3;
                else if (total == 13 || total == 29)
                    won = amount * 4;
                else if (total == 12 || total == 30)
                    won = amount * 5;
                else if (total == 11 || total == 31)
                    won = amount * 8;
                else if (total == 10 || total == 32)
                    won = amount * 10;
                else if (total == 9 || total == 33)
                    won = amount * 15;
                else if (total == 8 || total == 34)
                    won = amount * 20;
                else if (total == 7 || total == 35)
                    won = amount * 50;
                else
                    won = amount * 100;
                
                using (var bitmap = die.Merge(out var format))
                using (var ms = bitmap.ToStream(format))
                {
                    foreach (var dice in die)
                    {
                        dice.Dispose();
                    }

                    await ctx.Channel.SendFileAsync(ms, $"dice.{format.FileExtensions.First()}",
                        $"{ctx.User.Mention} You rolled a total of {total}");
                }
                if (won > 0)
                {
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithDescription($"{ctx.User.Mention} Congratulations! You've won {won} stones")).ConfigureAwait(false);
                    await _currency.ChangeAsync(ctx.User, "BetDie Payout", won, "Server", ctx.User.Id.ToString(), ctx.Guild.Id,
                        ctx.Channel.Id, ctx.Message.Id);
                    return;
                }
                
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithDescription($"{ctx.User.Mention} Better luck next time!")).ConfigureAwait(false);
            }

            private Image<Rgba32> GetDice(int num)
            {
                if (num < 0 || num > 6)
                    throw new ArgumentOutOfRangeException(nameof(num));

                var image = Image.Load(path + $"{num}.png");
                using (var ms = new MemoryStream())
                {
                    image.Save(ms, ImageFormats.Png);
                    return Image.Load(ms.ToArray());
                }
            }
        }
    }
}