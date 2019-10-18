using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface ILotteryRepository : IRepository<Lottery>
    {
        List<List<int>> GetLotteryEntries(ulong userId, string lotteryId);
        void AddLotteryEntry(ulong userId, List<int> numbers, string lotteryId);
        void NewLottery(ulong botId, List<int> numbers);
        string GetLotteryId();
    }

    public class LotteryRepository : Repository<Lottery>, ILotteryRepository
    {
        public LotteryRepository(DbContext context) : base(context)
        {
        }

        public List<List<int>> GetLotteryEntries(ulong userId, string lotteryId)
        {
            throw new System.NotImplementedException();
        }

        public void AddLotteryEntry(ulong userId, List<int> numbers, string lotteryId)
        {
            throw new System.NotImplementedException();
        }

        public void NewLottery(ulong botId, List<int> numbers)
        {
            throw new System.NotImplementedException();
        }

        public string GetLotteryId()
        {
            throw new System.NotImplementedException();
        }
    }
}