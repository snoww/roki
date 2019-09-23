using System.Linq;
using Discord;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IDUserRepository : IRepository<DUser>
    {
        void EnsureCreated(ulong userId, string username, string discriminator, string avatarId);
        DUser GetOrCreate(ulong userId, string username, string discriminator, string avatarId);
        DUser GetOrCreate(IUser original);
        DUser[] GetUsersXpLeaderboard(int page);
        long GetUserCurrency(ulong userId);
    }

    public class DUserRepository : Repository<DUser>, IDUserRepository
    {
        public DUserRepository(DbContext context) : base(context)
        {
        }

        public void EnsureCreated(ulong userId, string username, string discriminator, string avatarId)
        {
            var user = Set.FirstOrDefault(u => u.UserId == userId);

            if (user == null)
            {
                Context.Add(user = new DUser
                {
                    UserId = userId,
                    Username = username,
                    Discriminator = discriminator,
                    AvatarId = avatarId
                });
            }
//            Context.Database.ExecuteSqlCommand($@"
//UPDATE IGNORE User
//SET Username={username},
//    Discriminator={discriminator},
//    AvatarId={avatarId},
//Where UserId={userId};
//
//INSERT IGNORE INTO User (UserId, Username, Discriminator, AvatarId)
//VALUES ({userId}, {username}, {discriminator}, {avatarId});
//");
        }

        public DUser GetOrCreate(ulong userId, string username, string discriminator, string avatarId)
        {
            EnsureCreated(userId, username, discriminator, avatarId);
            return Set.First(u => u.UserId == userId);
        }

        public DUser GetOrCreate(IUser original) => 
            GetOrCreate(original.Id, original.Username, original.Discriminator, original.AvatarId);

        public DUser[] GetUsersXpLeaderboard(int page)
        {
            return Set
                .OrderByDescending(x => x.TotalXp)
                .Skip(page * 9)
                .Take(9)
                .AsEnumerable()
                .ToArray();
        }

        public long GetUserCurrency(ulong userId) =>
                Set.FirstOrDefault(x => x.UserId == userId)?.Currency ?? 0;
    }
}