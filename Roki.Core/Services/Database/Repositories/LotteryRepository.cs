using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface ILotteryRepository : IRepository<Lottery>
    {
        List<Lottery> GetLotteryEntries(ulong userId, string lotteryId);
        void AddLotteryEntry(ulong userId, List<int> numbers, string lotteryId);
        void NewLottery(ulong botId, List<int> numbers);
        string GetLotteryId();
    }

    public class LotteryRepository : Repository<Lottery>, ILotteryRepository
    {
        public LotteryRepository(DbContext context) : base(context)
        {
        }

        public List<Lottery> GetLotteryEntries(ulong userId, string lotteryId)
        {
            return Set.Where(l => l.UserId == userId && l.LotteryId == lotteryId).ToList();
        }

        public async void AddLotteryEntry(ulong userId, List<int> numbers, string lotteryId)
        {
            await Context.Database.ExecuteSqlCommandAsync($@"
INSERT INTO lottery(userId, num1, num2, num3, num4, num5, lotteryId)
VALUES({userId}, {numbers[0]}, {numbers[1]}, {numbers[2]}, {numbers[3]}, {numbers[4]}, {lotteryId})")
                .ConfigureAwait(false);
        }

        public async void NewLottery(ulong botId, List<int> numbers)
        {
            var lotteryId = Guid.NewGuid().ToString();
            await Context.Database.ExecuteSqlCommandAsync($@"
INSERT INTO lottery(userId, num1, num2, num3, num4, num5, lotteryId)
VALUES({botId}, {numbers[0]}, {numbers[1]}, {numbers[2]}, {numbers[3]}, {numbers[4]}, {lotteryId})")
                .ConfigureAwait(false);
        }

        public string GetLotteryId() =>
            Set.OrderByDescending(l => l.Id).First(u => u.Id == 549644503351296040).LotteryId;
    }
}