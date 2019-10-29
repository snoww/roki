using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Gambling.Services
{
    public class LotteryService : IRService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;
        private readonly ICurrencyService _currency;
        private readonly Random _rng = new Random();
        private Timer _timer;
        private const string Stone = "<:stone:269130892100763649>";
        private const ulong ChannelId = 222401767697154048;

        public LotteryService(DiscordSocketClient client, DbService db, ICurrencyService currency)
        {
            _client = client;
            _db = db;
            _currency = currency;
            LotteryTimer();
        }

        private void LotteryTimer()
        {
            var today = DateTime.UtcNow;
            var drawToday = new DateTime(today.Year, today.Month, today.Day, 23, 0, 0);
            var drawTmr = new DateTime(today.Year, today.Month, today.Day + 1, 23, 0, 0);
            // dueTime is when it first occurs, period is how long after each occurence

            _timer = drawToday - today > TimeSpan.Zero 
                ? new Timer(LotteryEvent, null, drawToday - today, TimeSpan.FromDays(1)) 
                : new Timer(LotteryEvent, null, drawTmr - today, TimeSpan.FromDays(1));
        }

        private async void LotteryEvent(object state)
        {
            var channel = _client.GetChannel(ChannelId) as IMessageChannel;

            using (var uow = _db.GetDbContext())
            {
                var jackpot = (long) (_currency.GetCurrency(549644503351296040) * 0.9);
                if (jackpot < 1000)
                    return;
                var lottery = uow.Lottery.GetLottery(_client.CurrentUser.Id);
                var winners = CheckWinner(uow.Lottery.GetAllLotteryEntries(lottery.LotteryId), new List<int>
                {
                    lottery.Num1,
                    lottery.Num2,
                    lottery.Num3,
                    lottery.Num4,
                    lottery.Num5,
                    lottery.Num6
                });
                var winningNum = $"The winning numbers are: {lottery.Num1}-{lottery.Num2}-{lottery.Num3}-{lottery.Num4}-{lottery.Num5}-{lottery.Num6}\n";
                if (winners.Count == 0)
                {
                    await channel.SendErrorAsync(winningNum + "No winners this draw").ConfigureAwait(false);
                    await uow.Lottery.NewLottery(_client.CurrentUser.Id, GenerateLotteryNumber()).ConfigureAwait(false);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    return;
                }
                var winStr = await GiveWinnings(winners).ConfigureAwait(false);
                await channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription(winningNum + winStr)).ConfigureAwait(false);
                await uow.Lottery.NewLottery(_client.CurrentUser.Id, GenerateLotteryNumber()).ConfigureAwait(false);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        private async Task<string> GiveWinnings(IEnumerable<Tuple<ulong, int>> winners)
        {
            
            var four = new List<ulong>();
            var five = new List<ulong>();
            var six = new List<ulong>();
            foreach (var (userId, correct) in winners)
            {
                if (correct == 4)
                {
                    four.Add(userId);
                }
                else if (correct == 5)
                {
                    five.Add(userId);
                }
                else
                {
                    six.Add(userId);
                }
            }

            var toReturn = "";
            using (var uow = _db.GetDbContext())
            {
                if (six.Count > 0)
                {
                    var amount = (long) (_currency.GetCurrency(_client.CurrentUser.Id) * 0.9) / six.Count;
                    toReturn += "JACKPOT WINNERS\n";
                    foreach (var winner in six)
                    {
                        await uow.DUsers.LotteryAwardAsync(winner, amount).ConfigureAwait(false);
                        await uow.DUsers.UpdateBotCurrencyAsync(_client.CurrentUser.Id, -amount);
                        toReturn += $"<@{winner}> WON {amount} {Stone}\n";
                    }
                    
                }
                if (five.Count > 0)
                {
                    toReturn += "5/6 Winners\n";
                    var amount = (long) (_currency.GetCurrency(_client.CurrentUser.Id) * 0.045) / five.Count;
                    foreach (var winner in five)
                    {
                        await uow.DUsers.LotteryAwardAsync(winner, amount).ConfigureAwait(false);
                        await uow.DUsers.UpdateBotCurrencyAsync(_client.CurrentUser.Id, -amount);
                        toReturn += $"<@{winner}> won {amount} {Stone}\n";
                    }
                }
                if (four.Count > 0)
                {
                    toReturn += "4/6 Winners\n";
                    var amount = (long) (_currency.GetCurrency(_client.CurrentUser.Id) * 0.055) / four.Count;
                    foreach (var winner in four)
                    {
                        await uow.DUsers.LotteryAwardAsync(winner, amount).ConfigureAwait(false);
                        await uow.DUsers.UpdateBotCurrencyAsync(_client.CurrentUser.Id, -amount);
                        toReturn += $"<@{winner}> won {amount} {Stone}\n";
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
                var num = _rng.Next(1, 41);
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
            return entries.Count == 0 ? null : entries.Select(entry => $"{entry.Num1}-{entry.Num2}-{entry.Num3}-{entry.Num4}-{entry.Num5}-{entry.Num6}").ToList();
        }

        private static List<Tuple<ulong, int>> CheckWinner(IEnumerable<Lottery> entries, List<int> winning)
        {
            var winners = new List<Tuple<ulong, int>>();
            foreach (var entry in entries)
            {
                if (entry.UserId == 549644503351296040)
                    continue;
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
                if (winning.Contains(entry.Num6))
                    count += 1;
                if (count > 3)
                    winners.Add(new Tuple<ulong, int>(entry.UserId, count));
            }

            return winners;
        }

        public static bool ValidNumbers(IEnumerable<int> numbers)
        {
            var list = numbers.ToList();
            var hs = new HashSet<int>();
            var valid = list.Any(t => !hs.Add(t));
            return valid && hs.Max() <= 40 && hs.Min() >= 1;
        }
    }
}