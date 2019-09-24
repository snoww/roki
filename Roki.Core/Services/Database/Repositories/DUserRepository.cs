using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;
using Roki.Core.Services.Impl;
using Roki.Modules.Xp;
using Roki.Modules.Xp.Common;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IDUserRepository : IRepository<DUser>
    {
        void EnsureCreated(ulong userId, string username, string discriminator, string avatarId);
        DUser GetOrCreate(ulong userId, string username, string discriminator, string avatarId);
        DUser GetOrCreate(IUser original);
        DUser[] GetUsersXpLeaderboard(int page);
        long GetUserCurrency(ulong userId);
        Task UpdateXp(DUser dUser, SocketMessage message);
    }

    public class DUserRepository : Repository<DUser>, IDUserRepository
    {
        public DUserRepository(DbContext context) : base(context)
        {
        }

        public void EnsureCreated(ulong userId, string username, string discriminator, string avatarId)
        {
//            var user = Set.FirstOrDefault(u => u.UserId == userId);
//
//            if (user == null)
//            {
//                Context.Update(user = new DUser
//                {
//                    UserId = userId,
//                    Username = username,
//                    Discriminator = discriminator,
//                    AvatarId = avatarId
//                });
//            }
            Context.Database.ExecuteSqlCommand($@"
UPDATE IGNORE users
SET Username={username},
    Discriminator={discriminator},
    AvatarId={avatarId}
WHERE UserId={userId};

INSERT IGNORE INTO users (UserId, Username, Discriminator, AvatarId, LastLevelUp, LastXpGain)
VALUES ({userId}, {username}, {discriminator}, {avatarId}, {DateTime.MinValue}, {DateTime.MinValue});
");
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

        public async Task UpdateXp(DUser dUser, SocketMessage message)
        {
            var user = Set.FirstOrDefault(u => u.Equals(dUser));
            if (user == null) return;
            var level = new XpLevel(user.TotalXp);
            // TODO lower xp per message afterwards
            var xp = user.TotalXp + 5;
            var newLevel = new XpLevel(xp);
            if (newLevel.Level > level.Level)
            {
                await Context.Database.ExecuteSqlCommandAsync($@"
UPDATE IGNORE users
SET TotalXp={xp},
    LastLevelUp={DateTime.UtcNow},
    LastXpGain={DateTime.UtcNow}
WHERE UserId={user.UserId};
").ConfigureAwait(false);
                
                await message.Channel.SendMessageAsync($"Congratulations @{user.UserId}! You've reached Level {new XpLevel(user.TotalXp).Level}").ConfigureAwait(false);
            }
            
            await Context.Database.ExecuteSqlCommandAsync($@"
UPDATE IGNORE users
SET TotalXp={xp},
    LastXpGain={DateTime.UtcNow}
WHERE UserId={user.UserId};
").ConfigureAwait(false);
        }
    }
}