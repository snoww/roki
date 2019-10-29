using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
        Task<bool> UpdateCurrencyAsync(IUser user, long amount);
        Task LotteryAwardAsync(ulong userId, long amount);
        Task UpdateBotCurrencyAsync(ulong botId, long amount);
        IEnumerable<DUser> GetCurrencyLeaderboard(ulong botId, int page);
        Task UpdateXp(DUser dUser, SocketMessage message);
        Task ChangeNotificationLocation(ulong userId, byte notify);
    }

    public class DUserRepository : Repository<DUser>, IDUserRepository
    {
        private readonly Properties _properties = JsonSerializer.Deserialize<Properties>("./data/properties.json");
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
            var user = Set.FirstOrDefault(u => u.UserId == userId);
            if (user != null && user.Username == username && user.Discriminator == discriminator && user.AvatarId == avatarId) 
                return user;
            
            EnsureCreated(userId, username, discriminator, avatarId);
            user = Set.First(u => u.UserId == userId);

            return user;
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

        public async Task<bool> UpdateCurrencyAsync(IUser user, long amount)
        {
            if (amount == 0)
                return false;
            var dUser = GetOrCreate(user);
            if (dUser.Currency + amount < 0)
                return false;
            await Context.Database.ExecuteSqlCommandAsync($@"
UPDATE IGNORE users
SET Currency=Currency+{amount}
WHERE UserId={dUser.UserId}
").ConfigureAwait(false);
            return true;
        }

        public async Task LotteryAwardAsync(ulong userId, long amount)
        {
            await Context.Database.ExecuteSqlCommandAsync($@"
UPDATE IGNORE users
SET Currency=Currency+{amount}
WHERE UserId={userId}
").ConfigureAwait(false);
        }

        public async Task UpdateBotCurrencyAsync(ulong botId, long amount)
        {
            await Context.Database.ExecuteSqlCommandAsync($@"
UPDATE IGNORE users
SET Currency=Currency+{amount}
WHERE UserId={botId}
").ConfigureAwait(false);
        }

        public IEnumerable<DUser> GetCurrencyLeaderboard(ulong botId, int page)
        {
            return Set.Where(c => c.Currency > 0 && botId != c.UserId)
                .OrderByDescending(c => c.Currency)
                .Skip(page * 9)
                .Take(9)
                .ToList();
        }

        public async Task UpdateXp(DUser dUser, SocketMessage message)
        {
            var user = Set.FirstOrDefault(u => u.Equals(dUser));
            if (user == null) return;
            var level = new XpLevel(user.TotalXp);
            // TODO lower xp per message afterwards
            var xp = user.TotalXp + _properties.XpPerMessage;
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
                
                await SendNotification(user, message, new XpLevel(xp).Level).ConfigureAwait(false);
            }
            
            await Context.Database.ExecuteSqlCommandAsync($@"
UPDATE IGNORE users
SET TotalXp={xp},
    LastXpGain={DateTime.UtcNow}
WHERE UserId={user.UserId};
").ConfigureAwait(false);
        }

        public async Task ChangeNotificationLocation(ulong userId, byte notify)
        {
            await Context.Database.ExecuteSqlCommandAsync($@"
UPDATE IGNORE users
SET NotificationLocation={notify}
WHERE UserId={userId}
").ConfigureAwait(false);
        }

        private static async Task SendNotification(DUser user, SocketMessage msg, int level)
        {
            switch (user.NotificationLocation)
            {
                case 0:
                    return;
                case 1:
                    var dm = await msg.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                    await dm.SendMessageAsync($"Congratulations {msg.Author.Mention}! You've reached Level {level}").ConfigureAwait(false);
                    return;
                case 2:
                    await msg.Channel.SendMessageAsync($"Congratulations {msg.Author.Mention}! You've reached Level {level}").ConfigureAwait(false);
                    return;
            }
        }
    }
}