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
            public async Task JoinLotteryMulti(int tickets = 2)
            {
                if (tickets < 2)
                {
                    await ctx.Channel.SendErrorAsync("Needs to buy at least 2 tickets.");
                    return;
                }
                var user = ctx.User;
                var removed = await _currency.ChangeAsync(ctx.User, $"Lottery Entry x{tickets}", -tickets, ctx.User.Id.ToString(), $"{ctx.Client.CurrentUser.Id}",
                    ctx.Guild.Id, ctx.Channel.Id, ctx.Message.Id).ConfigureAwait(false);
                if (!removed)
                {
                    await ctx.Channel.SendErrorAsync($"{ctx.User.Mention} you do not have enough currency to join the lottery.")
                        .ConfigureAwait(false);
                    return;
                }
                
                using (var uow = _db.GetDbContext())
                {
                    var lotteryId = uow.Lottery.GetLotteryId();
                    var numbers = new List<string>();
                    for (int i = 0; i < tickets; i++)
                    {
                        var number = _service.GenerateLotteryNumber();
                        uow.Lottery.AddLotteryEntry(user.Id, number, lotteryId);
                        if (i < 10)
                        {
                            numbers.Add(string.Join("-", number));
                        }
                    }
                    var entries = uow.Lottery.GetTotalEntries(user.Id, lotteryId);
                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle($"Purchase Successful - You have {entries} total entries")
                        .WithDescription($"{user.Mention} Here are your lottery numbers: `{string.Join('\n', numbers)}`")
                        .WithFooter("Note: Only shows first 10 tickets.");

                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }
            }
            
            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task JoinLottery(int[] nums = null)
            {
                if (nums != null && nums.Length != 6 && !LotteryService.ValidNumbers(nums))
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

                List<int> numbers;
                if (nums == null)
                    numbers = _service.GenerateLotteryNumber();
                else
                {
                    numbers = nums.ToList();
                    numbers.Sort();
                }
                
                using (var uow = _db.GetDbContext())
                {
                    var lotteryId = uow.Lottery.GetLotteryId();
                    uow.Lottery.AddLotteryEntry(user.Id, numbers, lotteryId);
                    var entries = uow.Lottery.GetTotalEntries(user.Id, lotteryId);
                    var embed = new EmbedBuilder().WithOkColor()
                        .WithDescription($"{user.Mention} Here's your lottery number: `{string.Join('-', numbers)}`\n");
                    embed.WithAuthor(entries == 1
                        ? $"Purchase Successful - You have {entries} entry in the lottery."
                        : $"Purchase Successful - You have {entries} total entries in the lottery.");
                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }
            }

            [RokiCommand, Description, Usage, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Tickets(int page = 1)
            {
                if (page <= 0)
                    return;
                using (var uow = _db.GetDbContext())
                {
                    var lotteryId = uow.Lottery.GetLotteryId();
                    var entries = _service.EntriesToListString(uow.Lottery.GetLotteryEntries(ctx.User.Id, lotteryId, page - 1));
                    if (entries.Count == 0)
                    {
                        await ctx.Channel.SendErrorAsync("You have no tickets for the current lottery.").ConfigureAwait(false);
                    }

                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle($"{ctx.User.Username}'s Lottery Tickets - Total {uow.Lottery.GetTotalEntries(ctx.User.Id, lotteryId)}")
                            .WithDescription($"`{string.Join("\n", entries)}`")
                            .WithFooter($"Page {page}"))
                        .ConfigureAwait(false);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }
            }
        }
    }
}