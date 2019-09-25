using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Roki.Core.Services;

namespace Roki.Services
{
    public interface ICurrencyService : IRService
    {
        Task ChangeAsync(ulong userId, string reason, long amount);
        Task ChangeAsync(IUser user, string reason, long amount);
        Task ChangeListAsync(IEnumerable<ulong> userIds, IEnumerable<string> reasons, IEnumerable<long> amounts);
    }
    
    public class CurrencyService : ICurrencyService
    {
        public Task ChangeAsync(ulong userId, string reason, long amount)
        {
            throw new System.NotImplementedException();
        }

        public Task ChangeAsync(IUser user, string reason, long amount)
        {
            throw new System.NotImplementedException();
        }

        public Task ChangeListAsync(IEnumerable<ulong> userIds, IEnumerable<string> reasons, IEnumerable<long> amounts)
        {
            throw new System.NotImplementedException();
        }
    }
}