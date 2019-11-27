using Roki.Core.Services;

namespace Roki.Modules.Gambling.Services
{
    public class GamblingService : IRService
    {
        private readonly DbService _db;

        public GamblingService(DbService db)
        {
            _db = db;
        }

        public long GetCurrency(ulong userId)
        {
            using var uow = _db.GetDbContext();
            return uow.Users.GetUserCurrency(userId);
        }
    }
}