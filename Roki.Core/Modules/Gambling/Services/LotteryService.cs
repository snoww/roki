using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using Discord.Commands;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Gambling.Services
{
    public class LotteryService : IRService
    {
        private readonly DbService _db;
        private readonly ICurrencyService _currency;
        private readonly ICommandContext _ctx;
        private readonly Random _rng = new Random();
        private const string Stone = "<:stone:269130892100763649>";


        public LotteryService(DbService db, ICurrencyService currency, ICommandContext ctx)
        {
            _db = db;
            _ctx = ctx;
            _currency = currency;
            StartTimer();
        }

        private void StartTimer()
        {
            var lotteryTimer = new Timer();
            lotteryTimer.Elapsed += LotteryEventHandler;
//            lotteryTimer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;
// TESTING TIME
            lotteryTimer.Interval = TimeSpan.FromMinutes(5).TotalMilliseconds;
            lotteryTimer.Enabled = true;
        }

        private async void LotteryEventHandler(object source, ElapsedEventArgs e)
        {
            using (var uow = _db.GetDbContext())
            {
                var lottery = uow.Lottery.GetLottery(_ctx.Client.CurrentUser.Id);
                var winners = CheckWinner(uow.Lottery.GetAllLotteryEntries(lottery.LotteryId), new List<int>
                {
                    lottery.Num1,
                    lottery.Num2,
                    lottery.Num3,
                    lottery.Num4,
                    lottery.Num5
                });

                if (winners.Count == 0)
                {
                    await _ctx.Channel.SendErrorAsync("No winners this draw").ConfigureAwait(false);
                    uow.Lottery.NewLottery(_ctx.Client.CurrentUser.Id, GenerateLotteryNumber());
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    return;
                }
                // TODO HERE
                var winStr = await GiveWinnings(winners).ConfigureAwait(false);
                await _ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription(winStr)).ConfigureAwait(false);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task<string> GiveWinnings(List<Tuple<ulong, int>> winners)
        {
            
            var three = new List<ulong>();
            var four = new List<ulong>();
            var five = new List<ulong>();
            foreach (var (userId, correct) in winners)
            {
                if (correct == 3)
                {
                    three.Add(userId);
                }
                else if (correct == 4)
                {
                    four.Add(userId);
                }
                else
                {
                    five.Add(userId);
                }
            }

            var toReturn = "";
            using (var uow = _db.GetDbContext())
            {
                if (five.Count > 0)
                {
                    var amount = (long) (_currency.GetCurrency(_ctx.Client.CurrentUser.Id) * 0.9) / five.Count;
                    toReturn += "JACKPOT WINNERS\n";
                    foreach (var winner in five)
                    {
                        await uow.DUsers.LotteryAwardAsync(winner, amount).ConfigureAwait(false);
                        await uow.DUsers.UpdateBotCurrencyAsync(_ctx.Client.CurrentUser.Id, -amount);
                        toReturn += $"<@{winner}> WON {amount} {Stone}";
                    }
                    
                }
                if (four.Count > 0)
                {
                    toReturn += "4/5 Winners\n";
                    var amount = (long) (_currency.GetCurrency(_ctx.Client.CurrentUser.Id) * 0.045) / four.Count;
                    foreach (var winner in four)
                    {
                        await uow.DUsers.LotteryAwardAsync(winner, amount).ConfigureAwait(false);
                        await uow.DUsers.UpdateBotCurrencyAsync(_ctx.Client.CurrentUser.Id, -amount);
                        toReturn += $"<@{winner}> won {amount} {Stone}";
                    }
                }
                if (three.Count > 0)
                {
                    toReturn += "3/5 Winners\n";
                    var amount = (long) (_currency.GetCurrency(_ctx.Client.CurrentUser.Id) * 0.055) / three.Count;
                    foreach (var winner in three)
                    {
                        await uow.DUsers.LotteryAwardAsync(winner, amount).ConfigureAwait(false);
                        await uow.DUsers.UpdateBotCurrencyAsync(_ctx.Client.CurrentUser.Id, -amount);
                        toReturn += $"<@{winner}> won {amount} {Stone}";
                    }
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
            }

            return toReturn;
        }

        public List<int> GenerateLotteryNumber()
        {
            var numList = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                var num = _rng.Next(1, 26);
                if (numList.Contains(num))
                {
                    i--;
                    continue;
                }
                numList.Add(num);
            }
            numList.Sort();
            
            return numList;
        }

        public List<string> EntriesToListString(IReadOnlyCollection<Lottery> entries)
        {
            return entries.Count == 0 ? null : entries.Select(entry => $"{entry.Num1}-{entry.Num2}-{entry.Num3}-{entry.Num4}-{entry.Num5}").ToList();
        }

        public List<Tuple<ulong, int>> CheckWinner(IEnumerable<Lottery> entries, List<int> winning)
        {
            var winners = new List<Tuple<ulong, int>>();
            foreach (var entry in entries)
            {
                var count = 0;
                if (winning.Contains(entry.Num1))
                    count += 1;
                if (winning.Contains(entry.Num2))
                    count += 1;
                if (winning.Contains(entry.Num3))
                    count += 1;;
                if (winning.Contains(entry.Num4))
                    count += 1;
                if (winning.Contains(entry.Num5))
                    count += 1;
                if (count > 2)
                    winners.Add(new Tuple<ulong, int>(entry.UserId, count));
            }

            return winners;
        }

        public bool ValidNumbers(IEnumerable<int> numbers)
        {
            var list = numbers.ToList();
            var hs = new HashSet<int>();
            var valid = list.Any(t => !hs.Add(t));
            return valid && hs.Max() <= 25 && hs.Min() >= 1;
        }
    }
}