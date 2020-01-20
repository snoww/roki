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
using Roki.Modules.Xp.Common;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User> GetOrCreateUserAsync(IUser user);
        Task<User> GetUserAsync(ulong userId);
        User[] GetUsersXpLeaderboard(int page);
        long GetUserCurrency(ulong userId);
        Task<bool> UpdateCurrencyAsync(ulong userId, long amount);
        Task LotteryAwardAsync(ulong userId, long amount);
        Task UpdateBotCurrencyAsync(ulong botId, long amount);
        IEnumerable<User> GetCurrencyLeaderboard(ulong botId, int page);
        Task UpdateXp(User user, SocketMessage message, bool boost = false);
        int GetUserXpRank(ulong userId);
        Task ChangeNotificationLocation(ulong userId, string notify);
        Task<List<Item>> GetUserInventory(ulong userId);
        Task<bool> UpdateUserInventory(ulong userId, string name, int quantity);
        Task<List<Investment>> GetUserPortfolioAsync(ulong userId);
        Task<bool> UpdateUserPortfolio(ulong userId, string symbol, string position, string action, long shares);
        Task UpdateUserPortfolio(ulong userId, List<Investment> investments);
        decimal GetUserInvestingAccount(ulong userId);
        Task<bool> UpdateInvestingAccountAsync(ulong userId, decimal amount);
        Task<bool> TransferToFromInvestingAccountAsync(ulong userId, decimal amount);
        Task<Dictionary<ulong, List<Investment>>> GetAllPortfolios();
        Task ChargeInterestAsync(ulong userId, decimal amount);
    }

    public class UserRepository : Repository<User>, IUserRepository
    {
        private readonly Properties _properties = JsonSerializer.Deserialize<Properties>(File.ReadAllText("./data/properties.json"));
        public UserRepository(DbContext context) : base(context)
        {
        }
        public async Task<User> GetOrCreateUserAsync(IUser user)
        {
            var existing = await Set.FirstOrDefaultAsync(u => u.UserId == user.Id).ConfigureAwait(false);
            if (existing != null) return existing;
            
            var usr = await Context.AddAsync(new User
            {
                UserId = user.Id,
                Username = user.Username,
                Discriminator = user.Discriminator,
                AvatarId = user.AvatarId,
            }).ConfigureAwait(false);
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return usr.Entity;
        }

        public async Task<User> GetUserAsync(ulong userId) =>
            await Set.FirstAsync(u => u.UserId == userId).ConfigureAwait(false);

        public User[] GetUsersXpLeaderboard(int page)
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

        public async Task<bool> UpdateCurrencyAsync(ulong userId, long amount)
        {
            if (amount == 0)
                return false;
            var dUser = await GetUserAsync(userId).ConfigureAwait(false);
            if (dUser.Currency + amount < 0)
                return false;
            dUser.Currency += amount;
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task LotteryAwardAsync(ulong userId, long amount)
        {
            var user = Set.First(u => u.UserId == userId);
            user.Currency += amount;
            await Context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task UpdateBotCurrencyAsync(ulong botId, long amount)
        {
            var bot = Set.First(b => b.UserId == botId);
            bot.Currency += amount;
            await Context.SaveChangesAsync().ConfigureAwait(false);
        }

        public IEnumerable<User> GetCurrencyLeaderboard(ulong botId, int page)
        {
            return Set.Where(c => c.Currency > 0 && botId != c.UserId)
                .OrderByDescending(c => c.Currency)
                .Skip(page * 9)
                .Take(9)
                .ToList();
        }

        public async Task UpdateXp(User user, SocketMessage message, bool boost = false)
        {
            if (user == null) return;
            var level = new XpLevel(user.TotalXp);
            int xp;
            if (boost)
                xp = _properties.XpPerMessage * 2;
            else 
                xp = _properties.XpPerMessage;
            var newLevel = new XpLevel(xp);
            user.TotalXp += xp;
            user.LastXpGain = DateTimeOffset.UtcNow;
            if (newLevel.Level > level.Level)
            {
                user.LastLevelUp = DateTimeOffset.UtcNow;
                await SendNotification(user, message, new XpLevel(xp).Level).ConfigureAwait(false);
            }

            await Context.SaveChangesAsync().ConfigureAwait(false);
        }

        public int GetUserXpRank(ulong userId)
        {
            return Set.Count(u => u.TotalXp >
                                  Set.Where(y => y.UserId == userId)
                                      .Select(y => y.TotalXp)
                                      .FirstOrDefault()) + 1;
        }

        public async Task ChangeNotificationLocation(ulong userId, string notify)
        {
            var user = await Set.FirstAsync(u => u.UserId == userId).ConfigureAwait(false);
            user.NotificationLocation = notify;
            await Context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<List<Item>> GetUserInventory(ulong userId)
        {
            var user = await Set.FirstAsync(u => u.UserId == userId).ConfigureAwait(false);
            return user.Inventory != null ? JsonSerializer.Deserialize<List<Item>>(user.Inventory) : null;
        }

        public async Task<bool> UpdateUserInventory(ulong userId, string name, int quantity)
        {
            var user = await Set.FirstAsync(u => u.UserId == userId).ConfigureAwait(false);
            var inv = await GetUserInventory(userId).ConfigureAwait(false);
            if (inv == null || inv.Count == 0)
            {
                Item[] inventory = {new Item {Name = name, Quantity = quantity}};
                var json = JsonSerializer.Serialize(inventory);
                user.Inventory = json;
            }
            else if (!inv.Any(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                var item = new Item {Name = name, Quantity = quantity};
                inv.Add(item);
                var json = JsonSerializer.Serialize(inv);
                user.Inventory = json;
            }
            else
            {
                var item = inv.First(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (item.Quantity + quantity < 0) return false;
                item.Quantity += quantity;
                if (item.Quantity == 0)
                    inv.Remove(item);
                var json = JsonSerializer.Serialize(inv);
                user.Inventory = json;
            }

            await Context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<List<Investment>> GetUserPortfolioAsync(ulong userId)
        {
            var user = await Set.FirstAsync(u => u.UserId == userId).ConfigureAwait(false);
            var portfolio = user.Portfolio != null ? JsonSerializer.Deserialize<List<Investment>>(user.Portfolio) : null;
            if (portfolio != null && portfolio.Count == 0) return null;
            return portfolio;
        }

        public async Task<bool> UpdateUserPortfolio(ulong userId, string symbol, string position, string action, long shares)
        {
            var user = await Set.FirstAsync(u => u.UserId == userId).ConfigureAwait(false);
            var portfolio = await GetUserPortfolioAsync(userId).ConfigureAwait(false);
            DateTimeOffset? interestDate = null;
            if (position == "short")
                interestDate = DateTimeOffset.UtcNow.AddDays(7);

            if (portfolio == null)
            {
                Investment[] investment = {new Investment {Symbol = symbol, Position = position, Shares = shares, InterestDate = interestDate}};
                var json = JsonSerializer.Serialize(investment);
                user.Portfolio = json;
            }
            else if (!portfolio.Any(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            {
                var investment = new Investment
                {
                    Symbol = symbol,
                    Position = position,
                    Shares = shares,
                    InterestDate = interestDate
                 };
                portfolio.Add(investment);
                var json = JsonSerializer.Serialize(portfolio);
                user.Portfolio = json;
            }
            else
            {
                var investment = portfolio.First(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
                if (investment.Shares + shares < 0) return false;
                investment.Shares += shares;
                if (investment.Shares == 0)
                    portfolio.Remove(investment);
                var json = JsonSerializer.Serialize(portfolio);
                user.Portfolio = json;
            }

            await Context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task UpdateUserPortfolio(ulong userId, List<Investment> investments)
        {
            var user = await Set.FirstAsync(u => u.UserId == userId);
            user.Portfolio = JsonSerializer.Serialize(investments);
            await Context.SaveChangesAsync().ConfigureAwait(false);
        }

        public decimal GetUserInvestingAccount(ulong userId)
        {
            return Set.First(u => u.UserId == userId).InvestingAccount;
        }

        public async Task<bool> UpdateInvestingAccountAsync(ulong userId, decimal amount)
        {
            var user = Set.First(u => u.UserId == userId);
            if (user.InvestingAccount + amount < 0) return false;
            user.InvestingAccount += amount;
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> TransferToFromInvestingAccountAsync(ulong userId, decimal amount)
        {
            var user = await Set.FirstAsync(u => u.UserId == userId).ConfigureAwait(false);
            var currencyAcc = user.Currency;
            var investAcc = user.InvestingAccount;
            if ((amount <= 0 || currencyAcc - amount < 0) && (amount >= 0 || investAcc + amount < 0)) return false;
            user.Currency -= (long) amount;
            user.InvestingAccount += amount;
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<Dictionary<ulong, List<Investment>>> GetAllPortfolios()
        {
            var users =  Set.Where(u => u.Portfolio != null).ToList();
            var portfolios = new Dictionary<ulong, List<Investment>>();
            foreach (var user in users)
            {
                var port = await GetUserPortfolioAsync(user.UserId).ConfigureAwait(false);
                if (port == null)
                    continue;
                portfolios.Add(user.UserId, port);
            }

            return portfolios;
        }

        public async Task ChargeInterestAsync(ulong userId, decimal amount)
        {
            var user = await Set.FirstAsync(u => u.UserId == userId).ConfigureAwait(false);
            if (user.InvestingAccount > 0)
                user.InvestingAccount -= amount;
            else
                user.Currency -= (long) amount;

            await Context.SaveChangesAsync().ConfigureAwait(false);
        }

        private static async Task SendNotification(User user, SocketMessage msg, int level)
        {
            if (user.NotificationLocation.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
            }
            else if (user.NotificationLocation.Equals("dm", StringComparison.OrdinalIgnoreCase))
            {
                var dm = await msg.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                await dm.SendMessageAsync($"Congratulations {msg.Author.Mention}! You've reached Level {level}").ConfigureAwait(false);
            }
            else if (user.NotificationLocation.Equals("server", StringComparison.OrdinalIgnoreCase))
            {
                await msg.Channel.SendMessageAsync($"Congratulations {msg.Author.Mention}! You've reached Level {level}").ConfigureAwait(false);
            }
        }
    }
}