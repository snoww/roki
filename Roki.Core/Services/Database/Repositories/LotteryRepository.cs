using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;
using Roki.Modules.Gambling;

namespace Roki.Core.Services.Database.Repositories
{
    public interface ILotteryRepository : IRepository<Lottery>
    {
        List<Lottery> GetLotteryEntries(ulong userId, string lotteryId, int page);
        int GetTotalEntries(ulong userId, string lotteryId);
        List<Lottery> GetAllLotteryEntries(string lotteryId);
        Task<bool> AddLotteryEntry(ulong userId, List<int> numbers, string lotteryId);
        Task<bool> NewLottery(ulong botId, List<int> numbers);
        Lottery GetLottery(ulong botId);
        string GetLotteryId();
        DateTimeOffset GetLotteryDate(ulong botId);
    }

    public class LotteryRepository : Repository<Lottery>, ILotteryRepository
    {
        public LotteryRepository(DbContext context) : base(context)
        {
        }

        public List<Lottery> GetLotteryEntries(ulong userId, string lotteryId, int page)
        {
            return Set.Where(l => l.UserId == userId && l.LotteryId == lotteryId).Skip(page * 9).Take(9).ToList();
        }

        public int GetTotalEntries(ulong userId, string lotteryId)
        {
            return Set.Count(l => l.UserId == userId && l.LotteryId == lotteryId);
        }

        public List<Lottery> GetAllLotteryEntries(string lotteryId)
        {
            return Set.Where(l => l.LotteryId == lotteryId).ToList();
        }

        public async Task<bool> AddLotteryEntry(ulong userId, List<int> numbers, string lotteryId)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO lottery(user_id, num_1, num_2, num_3, num_4, num_5, num_6, lottery_id, date)
VALUES({userId}, {numbers[0]}, {numbers[1]}, {numbers[2]}, {numbers[3]}, {numbers[4]}, {numbers[5]}, {lotteryId}, {DateTime.UtcNow})")
                .ConfigureAwait(false);
            return true;
        }

        public async Task<bool> NewLottery(ulong botId, List<int> numbers)
        {
            var lotteryId = Guid.NewGuid().ToString();
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO lottery(user_id, num_1, num_2, num_3, num_4, num_5, num_6, lottery_id, date)
VALUES({botId}, {numbers[0]}, {numbers[1]}, {numbers[2]}, {numbers[3]}, {numbers[4]}, {numbers[5]}, {lotteryId}, {DateTime.UtcNow})")
                .ConfigureAwait(false);
            return true;
        }

        public Lottery GetLottery(ulong botId)
        {
            return Set.OrderByDescending(l => l.Id).First(u => u.UserId == botId);
        }

        public string GetLotteryId() =>
            Set.OrderByDescending(l => l.Id).First(u => u.UserId == 549644503351296040).LotteryId;

        public DateTimeOffset GetLotteryDate(ulong botId) =>
            Set.OrderByDescending(l => l.Id).First(u => u.UserId == 549644503351296040).Date;
    }
}