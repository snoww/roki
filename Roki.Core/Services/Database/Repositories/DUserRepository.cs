using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
using Roki.Modules.Xp.Common;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IDUserRepository : IRepository<DUser>
    {
        Task<DUser> GetOrCreate(IUser original);
        DUser[] GetUsersXpLeaderboard(int page);
        long GetUserCurrency(ulong userId);
        Task<bool> UpdateCurrencyAsync(IUser user, long amount);
        Task LotteryAwardAsync(ulong userId, long amount);
        Task UpdateBotCurrencyAsync(ulong botId, long amount);
        IEnumerable<DUser> GetCurrencyLeaderboard(ulong botId, int page);
        Task UpdateXp(DUser user, SocketMessage message, bool boost = false);
        Task ChangeNotificationLocation(ulong userId, string notify);
        Task<List<Item>> GetUserInventory(ulong userId);
        Task<bool> UpdateUserInventory(ulong userId, string name, int quantity);
        Task<List<Investment>> GetUserPortfolio(ulong userId);
        Task<bool> UpdateUserPortfolio(ulong userId, string symbol, string position, string action, long shares);
        decimal GetUserInvestingAccount(ulong userId);
        Task<bool> UpdateInvestingAccountAsync(ulong userId, decimal amount);
        Task<bool> TransferToFromInvestingAccountAsync(ulong userId, decimal amount);
        Task<Dictionary<ulong, List<Investment>>> GetAllPortfolios();
        Task ChargeInterestAsync(ulong userId, decimal amount);
    }

    public class DUserRepository : Repository<DUser>, IDUserRepository
    {
        private readonly Properties _properties = JsonSerializer.Deserialize<Properties>(File.ReadAllText("./data/properties.json"));
        public DUserRepository(DbContext context) : base(context)
        {
        }

        private async Task<DUser> EnsureCreated(ulong userId, string username, string discriminator, string avatarId)
        {
            var user = Set.FirstOrDefault(u => u.UserId == userId);

            if (user == null)
            {
                Context.Add(user = new DUser
                {
                    UserId = userId,
                    Username = username,
                    Discriminator = discriminator,
                    AvatarId = avatarId,
                });
            }
            else if (username != user.Username || discriminator != user.Discriminator || avatarId != user.AvatarId)
            {
                user.Username = username;
                user.Discriminator = discriminator;
                user.AvatarId = avatarId;
            }

            await Context.SaveChangesAsync().ConfigureAwait(false);
            return user;
        }

        public async Task<DUser> GetOrCreate(IUser original) =>
            await EnsureCreated(original.Id, original.Username, original.Discriminator, original.AvatarId).ConfigureAwait(false);

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
            var dUser = await GetOrCreate(user).ConfigureAwait(false);
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

        public IEnumerable<DUser> GetCurrencyLeaderboard(ulong botId, int page)
        {
            return Set.Where(c => c.Currency > 0 && botId != c.UserId)
                .OrderByDescending(c => c.Currency)
                .Skip(page * 9)
                .Take(9)
                .ToList();
        }

        public async Task UpdateXp(DUser user, SocketMessage message, bool boost = false)
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

        public async Task<List<Investment>> GetUserPortfolio(ulong userId)
        {
            var user = await Set.FirstAsync(u => u.UserId == userId).ConfigureAwait(false);
            return user.Portfolio != null ? JsonSerializer.Deserialize<List<Investment>>(user.Portfolio) : null;
        }

        public async Task<bool> UpdateUserPortfolio(ulong userId, string symbol, string position, string action, long shares)
        {
            var user = await Set.FirstAsync(u => u.UserId == userId).ConfigureAwait(false);
            var portfolio = await GetUserPortfolio(userId).ConfigureAwait(false);
            DateTimeOffset? interestDate = null;
            if (position == "short")
            {
                shares = -shares;
                interestDate = DateTimeOffset.UtcNow.AddDays(7);;
            }
            
            if (portfolio == null || portfolio.Count == 0)
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
                if (investment.Position == "short" && action == "buy")
                    shares = -shares;
                else if (investment.Position == "short" && action == "sell")
                    shares = Math.Abs(shares);
                else if (investment.Position == "long" && action == "buy")
                    shares = Math.Abs(shares);
                else
                    shares = -shares;
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
                var port = await GetUserPortfolio(user.UserId).ConfigureAwait(false);
                if (port == null || port.Count <= 0)
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

        private static async Task SendNotification(DUser user, SocketMessage msg, int level)
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