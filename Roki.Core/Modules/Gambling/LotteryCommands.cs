using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
using Roki.Modules.Gambling.Services;
using Roki.Services;

namespace Roki.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class LotteryCommands : RokiSubmodule<LotteryService>
        {
            private readonly ICurrencyService _currency;
            private readonly DbService _db;
            private const string Stone = "<:stone:269130892100763649>";
            
            public LotteryCommands(ICurrencyService currency, DbService db)
            {
                _currency = currency;
                _db = db;
            }
            
            [RokiCommand, Description, Usage, Aliases]
            public async Task Jackpot()
            {
                var jackpot = (long) (_currency.GetCurrency(ctx.Client.CurrentUser.Id) * 0.9);
                if (jackpot < 1000)
                    await ctx.Channel.SendErrorAsync("The lottery is currently down. Please check back another time.").ConfigureAwait(false);

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("Stone Lottery")
                        .WithDescription($"Current Jackpot: {Format.Bold(jackpot.ToString())} {Stone}"))
                    .ConfigureAwait(false);
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task JoinLottery(int[] nums = null)
            {
                if (nums != null && nums.Length != 6 && !_service.ValidNumbers(nums))
                {
                    await ctx.Channel.SendErrorAsync("Invalid numbers: Please enter 6 numbers from 1 to 30, no repeats.");
                    return;
                }
                var user = ctx.User;
                var removed = await _currency.ChangeAsync(ctx.User, "Lottery Entry", -1, ctx.User.Id.ToString(), $"{ctx.Client.CurrentUser.Id}",
                    ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id).ConfigureAwait(false);
                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync($"{ctx.User.Mention} you do not have enough currency to join the lottery.")
                        .ConfigureAwait(false);
                    return;
                }

                var numbers = nums == null ? _service.GenerateLotteryNumber() : nums.ToList();
                using (var uow = _db.GetDbContext())
                {
                    var lotteryId = uow.Lottery.GetLotteryId();
                    uow.Lottery.AddLotteryEntry(user.Id, numbers, lotteryId);
                    var entries = uow.Lottery.GetLotteryEntries(user.Id, lotteryId).Count;
                    var embed = new EmbedBuilder().WithOkColor();
                    embed.WithDescription(entries == 1
                        ? $"{user.Mention} you've joined the lottery.\n Here's your lottery number: `{string.Join('-', numbers)}`\nYou have {entries} entry in the lottery."
                        : $"{user.Mention} you've joined the lottery.\n Here's your lottery number: `{string.Join('-', numbers)}`\nYou have {entries} entries in the lottery.");

                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Tickets()
            {
                using (var uow = _db.GetDbContext())
                {
                    var lotteryId = uow.Lottery.GetLotteryId();
                    var entries = _service.EntriesToListString(uow.Lottery.GetLotteryEntries(ctx.User.Id, lotteryId));
                    if (entries.Count == 0)
                    {
                        await ctx.Channel.SendErrorAsync("You have no tickets for the current lottery.").ConfigureAwait(false);
                    }

                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithDescription($"{ctx.User.Mention} Here are you lottery ticket numbers:\n`{string.Join("\n", entries)}`"))
                        .ConfigureAwait(false);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }
            }


        }
    }
}