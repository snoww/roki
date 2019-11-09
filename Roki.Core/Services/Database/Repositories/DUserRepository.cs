using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
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
        Task UpdateXp(DUser dUser, SocketMessage message, bool boost = false);
        Task ChangeNotificationLocation(ulong userId, byte notify);
        Task<Inventory> GetOrCreateUserInventory(ulong userId);
        Task UpdateUserInventory(ulong userId, string key, int value);
        Task<List<Investment>> GetOrCreateUserPortfolio(ulong userId);
        Task<bool> UpdateUserPortfolio(ulong userId, string symbol, string position, long amount);
        long GetUserInvestingAccount(ulong userId);
        Task<bool> UpdateInvestingAccountAsync(ulong userId, long amount);
        Task<bool> TransferToFromInvestingAccountAsync(ulong userId, long amount);
        IEnumerable<DUser> GetAllPortfolios();
    }

    public class DUserRepository : Repository<DUser>, IDUserRepository
    {
        private readonly Properties _properties = JsonSerializer.Deserialize<Properties>(File.ReadAllText("./data/properties.json"));
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
            Context.Database.ExecuteSqlInterpolated($@"
UPDATE IGNORE users
SET Username={username},
    Discriminator={discriminator},
    AvatarId={avatarId}
WHERE UserId={userId};

INSERT IGNORE INTO users (UserId, Username, Discriminator, AvatarId, LastLevelUp, LastXpGain, InvestingAccount)
VALUES ({userId}, {username}, {discriminator}, {avatarId}, {DateTime.MinValue}, {DateTime.MinValue}, 50000);
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
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET Currency=Currency+{amount}
WHERE UserId={dUser.UserId}
").ConfigureAwait(false);
            return true;
        }

        public async Task LotteryAwardAsync(ulong userId, long amount)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET Currency=Currency+{amount}
WHERE UserId={userId}
").ConfigureAwait(false);
        }

        public async Task UpdateBotCurrencyAsync(ulong botId, long amount)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
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

        public async Task UpdateXp(DUser dUser, SocketMessage message, bool boost = false)
        {
            var user = Set.FirstOrDefault(u => u.Equals(dUser));
            if (user == null) return;
            var level = new XpLevel(user.TotalXp);
            int xp;
            if (boost)
                xp = user.TotalXp + _properties.XpPerMessage * 2;
            else 
                xp = user.TotalXp + _properties.XpPerMessage;
            var newLevel = new XpLevel(xp);
            if (newLevel.Level > level.Level)
            {
                await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET TotalXp={xp},
    LastLevelUp={DateTime.UtcNow},
    LastXpGain={DateTime.UtcNow}
WHERE UserId={user.UserId};
").ConfigureAwait(false);
                
                await SendNotification(user, message, new XpLevel(xp).Level).ConfigureAwait(false);
                return;
            }
            
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET TotalXp={xp},
    LastXpGain={DateTime.UtcNow}
WHERE UserId={user.UserId};
").ConfigureAwait(false);
        }

        public async Task ChangeNotificationLocation(ulong userId, byte notify)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET NotificationLocation={notify}
WHERE UserId={userId}
").ConfigureAwait(false);
        }

        public async Task<Inventory> GetOrCreateUserInventory(ulong userId)
        {
            var user = Set.First(u => u.UserId == userId);
            if (user.Inventory != null) return JsonSerializer.Deserialize<Inventory>(user.Inventory);
            var inv = new Inventory();
            var json = JsonSerializer.Serialize(inv);
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET Inventory={json}
WHERE UserId={userId}")
                .ConfigureAwait(false);

            return inv;
        }

        public async Task UpdateUserInventory(ulong userId, string key, int value)
        {
            var inv = await GetOrCreateUserInventory(userId).ConfigureAwait(false);
            inv.UpdateJsonProperty(key, value);
            var json = JsonSerializer.Serialize(inv);
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET Inventory={json}
WHERE UserId={userId}")
                .ConfigureAwait(false);
        }

        public async Task<List<Investment>> GetOrCreateUserPortfolio(ulong userId)
        {
            var user = Set.First(u => u.UserId == userId);
            if (user.Portfolio != null) return JsonSerializer.Deserialize<List<Investment>>(user.Portfolio);
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET Portfolio=[]
WHERE UserId={userId}");
            return null;
        }

        public async Task<bool> UpdateUserPortfolio(ulong userId, string symbol, string position, long amount)
        {
            var portfolio = await GetOrCreateUserPortfolio(userId).ConfigureAwait(false);
            if (portfolio == null)
            {
                Investment[] investment = {new Investment
                {
                    Symbol = symbol,
                    Position = position,
                    Shares = amount
                }};
                var json = JsonSerializer.Serialize(investment);
                await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET Portfolio={json}
WHERE UserId={userId}")
                    .ConfigureAwait(false);
            }
            else if (!portfolio.Any(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            {
                var investment = new Investment
                {
                    Symbol = symbol,
                    Position = position,
                    Shares = amount
                };
                portfolio.Add(investment);
                var json = JsonSerializer.Serialize(portfolio);
                await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET Portfolio={json}
WHERE UserId={userId}").
                    ConfigureAwait(false);
            }
            else
            {
                var investment = portfolio.First(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
                if (investment.Shares + amount < 0) return false;
                investment.Shares += amount;
                if (investment.Shares == 0)
                    portfolio.Remove(investment);
                var json = JsonSerializer.Serialize(portfolio);
                await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET Portfolio={json}
WHERE UserId={userId}").
                    ConfigureAwait(false);
            }

            return true;
        }

        public long GetUserInvestingAccount(ulong userId)
        {
            return Set.First(u => u.UserId == userId).InvestingAccount;
        }

        public async Task<bool> UpdateInvestingAccountAsync(ulong userId, long amount)
        {
            var currentAmount = Set.First(u => u.UserId == userId).InvestingAccount;
            if (currentAmount + amount < 0) return false;
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET InvestingAccount={currentAmount + amount}
WHERE UserId={userId}")
                .ConfigureAwait(false);
            return true;
        }

        public async Task<bool> TransferToFromInvestingAccountAsync(ulong userId, long amount)
        {
            var user = Set.First(u => u.UserId == userId);
            var currencyAcc = user.Currency;
            var investAcc = user.InvestingAccount;
            // todo create transaction
            if (amount > 0 && currencyAcc - amount >= 0)
            {
                await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET Currency={currencyAcc - amount},
    InvestingAccount={investAcc + amount}
WHERE UserId={userId}")
                    .ConfigureAwait(false);
                return true;
            }

            if (amount <= 0 || investAcc + amount < 0) return false;
            await Context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IGNORE users
SET Currency={currencyAcc - amount},
    InvestingAccount={investAcc + amount}
WHERE UserId={userId}")
                .ConfigureAwait(false);
            return true;
        }

        public IEnumerable<DUser> GetAllPortfolios()
        {
            return Set.Where(u => u.Portfolio != null).ToList();
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