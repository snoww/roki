using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Roki.Modules.Games.Common;
using Roki.Services.Database.Maps;

namespace Roki.Services
{
    public interface IMongoContext
    {
        IMongoCollection<Message> MessageCollection { get; }
        IMongoCollection<Channel> ChannelCollection { get; }
        IMongoCollection<Guild> GuildCollection { get; }
        IMongoCollection<User> UserCollection { get; }
        IMongoCollection<Transaction> TransactionCollection { get; }
        IMongoCollection<JeopardyClue> JeopardyCollection { get; }

        
        Task<User> GetOrAddUserAsync(IUser user);
        Task<User> GetUserAsync(ulong userId);
        Task<User> UpdateUserAsync(IUser after);
        Task<int> UpdateUserXpAsync(User user, bool doubleXp);
        Task<bool> UpdateUserCurrencyAsync(User user, long amount);
        Task<bool> UpdateUserCurrencyAsync(ulong userId, long amount);
        Task UpdateBotCurrencyAsync(long amount);
        long GetBotCurrencyAsync();
        long GetUserCurrency(ulong userId);
        decimal GetUserInvesting(ulong userId);
        IEnumerable<User> GetCurrencyLeaderboard(int page);
        Task<bool> TransferCurrencyAsync(ulong userId, decimal amount);
        Task<bool> AddOrUpdateUserSubscriptionAsync(ulong userId, ulong guildId, ObjectId subId, int days);
        Task<Dictionary<ulong, List<Subscription>>> RemoveExpiredSubscriptionsAsync();
        Task<bool> AddOrUpdateUserInventoryAsync(ulong userId, ulong guildId, ObjectId itemId, int quantity);

        Task<Channel> GetOrAddChannelAsync(ITextChannel channel);
        Task DeleteChannelAsync(ITextChannel channel);
        Task UpdateChannelAsync(ITextChannel after);
        bool IsLoggingEnabled(ITextChannel channel);
        
        Task<Guild> GetOrAddGuildAsync(SocketGuild guild);
        Task<Guild> GetGuildAsync(ulong guildId);
        Task ChangeGuildAvailabilityAsync(SocketGuild guild, bool available);
        Task UpdateGuildAsync(SocketGuild after);
        Task AddStoreItemAsync(ulong guildId, Listing item);
        Task<Listing> GetStoreItemByNameAsync(ulong guildId, string name);
        Task<Listing> GetStoreItemByIdAsync(ulong guildId, ObjectId id);
        Task<List<Listing>> GetStoreCatalogueAsync(ulong guildId);
        Task UpdateStoreItemAsync(ulong guildId, string name, int amount);

        Task AddMessageAsync(SocketMessage message);
        Task AddMessageEditAsync(SocketMessage after);
        Task MessageDeletedAsync(ulong messageId);

        Task AddTransaction(Transaction transaction);
        IEnumerable<Transaction> GetTransactions(ulong userId, int page);

        Dictionary<string, List<JClue>> GetRandomJeopardyCategories(int number);

    }

    public class MongoContext : IMongoContext
    {
        private readonly IMongoDatabase _database;
        
        public IMongoCollection<Message> MessageCollection { get; }
        public IMongoCollection<Channel> ChannelCollection { get; }
        public IMongoCollection<Guild> GuildCollection { get; }
        public IMongoCollection<User> UserCollection { get; }
        public IMongoCollection<Transaction> TransactionCollection { get; }
        public IMongoCollection<JeopardyClue> JeopardyCollection { get; }

        public MongoContext(IMongoDatabase database)
        {
            _database = database;
            
            MessageCollection = database.GetCollection<Message>("messages");
            ChannelCollection = database.GetCollection<Channel>("channels");
            GuildCollection = database.GetCollection<Guild>("guilds");
            UserCollection = database.GetCollection<User>("users");
            TransactionCollection = database.GetCollection<Transaction>("transactions");
            JeopardyCollection = database.GetCollection<JeopardyClue>("jeopardy");
        }


        public async Task<User> GetOrAddUserAsync(IUser user)
        {
            var dbUser = await UserCollection.Find(u => u.Id == user.Id).FirstOrDefaultAsync().ConfigureAwait(false);
            if (dbUser != null)
            {
                return dbUser;
            }

            dbUser = new User{Id = user.Id, Discriminator = user.DiscriminatorValue, AvatarId = user.AvatarId, Username = user.Username};

            await UserCollection.InsertOneAsync(dbUser);
            return dbUser;
        }

        public async Task<User> GetUserAsync(ulong userId)
        {
            return await UserCollection.Find(u => u.Id == userId).FirstAsync();
        }

        public async Task<User> UpdateUserAsync(IUser after)
        {
            var updateUser = Builders<User>.Update.Set(u => u.Id, after.Id)
                .Set(u => u.Username, after.Username)
                .Set(u => u.Discriminator, int.Parse(after.Discriminator))
                .Set(u => u.AvatarId, after.AvatarId);

            return await UserCollection.FindOneAndUpdateAsync<User>(u => u.Id == after.Id, updateUser,
                new FindOneAndUpdateOptions<User>{ReturnDocument = ReturnDocument.After, IsUpsert = true}).ConfigureAwait(false);
        }

        public async Task<int> UpdateUserXpAsync(User user, bool doubleXp)
        {
            var updateXp = Builders<User>.Update.Inc(u => u.Xp,
                doubleXp ? Roki.Properties.XpPerMessage * 2 : Roki.Properties.XpPerMessage);
            
            return await UserCollection.FindOneAndUpdateAsync(u => u.Id == user.Id, updateXp,
                    new FindOneAndUpdateOptions<User, int> {ReturnDocument = ReturnDocument.After})
                .ConfigureAwait(false);
        }

        public async Task<bool> UpdateUserCurrencyAsync(User user, long amount)
        {
            if (user.Currency + amount < 0)
                return false;
            var updateCurrency = Builders<User>.Update.Inc(u => u.Currency, amount);
            await UserCollection.UpdateOneAsync(u => u.Id == user.Id, updateCurrency).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> UpdateUserCurrencyAsync(ulong userId, long amount)
        {
            var dbUser = await GetUserAsync(userId).ConfigureAwait(false);
            return await UpdateUserCurrencyAsync(dbUser, amount).ConfigureAwait(false);
        }

        public async Task UpdateBotCurrencyAsync(long amount)
        {
            var updateCurrency = Builders<User>.Update.Inc(u => u.Currency, amount);
            await UserCollection.UpdateOneAsync(u => u.Id == Roki.Properties.BotId, updateCurrency).ConfigureAwait(false);
        }

        public long GetBotCurrencyAsync()
        {
            return UserCollection.Find(u => u.Id == Roki.Properties.BotId).First().Currency;
        }

        public long GetUserCurrency(ulong userId)
        {
            return UserCollection.Find(u => u.Id == userId).First().Currency;
        }

        public decimal GetUserInvesting(ulong userId)
        {
            return UserCollection.Find(u => u.Id == userId).First().InvestingAccount;
        }

        public IEnumerable<User> GetCurrencyLeaderboard(int page)
        {
            return UserCollection.AsQueryable()
                .Where(u => u.Currency > 0 && u.Id != Roki.Properties.BotId)
                .OrderByDescending(u => u.Currency)
                .Skip(page * 9)
                .Take(9)
                .ToList();
        }

        public async Task<bool> TransferCurrencyAsync(ulong userId, decimal amount)
        {
            var user = await GetUserAsync(userId).ConfigureAwait(false);
            var cash = user.Currency;
            var invest = user.InvestingAccount;
            
            if ((amount <= 0 || cash - amount < 0) && (amount >= 0 || invest + amount < 0)) 
                return false;

            var update = Builders<User>.Update.Inc(u => u.Currency, -(long) amount)
                .Inc(u => u.InvestingAccount, amount);

            await UserCollection.UpdateOneAsync(u => u.Id == userId, update).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> AddOrUpdateUserSubscriptionAsync(ulong userId, ulong guildId, ObjectId subId, int days)
        {
            var user = await GetUserAsync(userId).ConfigureAwait(false);
            // if exists update then return true
            var sub = user.Subscriptions.FirstOrDefault(x => x.Id == subId);
            if (sub != null)
            {
                var newEndDate = sub.EndDate.AddDays(days).Date;
                var update = Builders<User>.Update.Set(x => x.Subscriptions[-1].EndDate, newEndDate);
                await UserCollection.UpdateOneAsync(x => x.Id == userId, update).ConfigureAwait(false);
                return true;
            }
            
            var newSub = Builders<User>.Update.Push(x => x.Subscriptions, new Subscription
            {
                Id = subId,
                GuildId = guildId,
                EndDate = DateTime.Now.AddDays(days).Date
            });
            
            await UserCollection.UpdateOneAsync(x => x.Id == userId, newSub).ConfigureAwait(false);
            return false;
        }

        public async Task<Dictionary<ulong, List<Subscription>>> RemoveExpiredSubscriptionsAsync()
        {
            var now = DateTime.UtcNow.Date;
            var expired = new Dictionary<ulong, List<Subscription>>();

            using (var cursor = await UserCollection.FindAsync(FilterDefinition<User>.Empty))
            {
                while (await cursor.MoveNextAsync())
                {
                    foreach (var user in cursor.Current)
                    {
                        expired.Add(user.Id, user.Subscriptions.Where(x => now >= x.EndDate).ToList());
                    }
                }
            }

            if (expired.Count == 0)
            {
                return expired;
            }
            
            var update = Builders<User>.Update.PullFilter(x => x.Subscriptions, y => now >= y.EndDate);
            await UserCollection.UpdateManyAsync(FilterDefinition<User>.Empty, update).ConfigureAwait(false);

            return expired;
        }

        public async Task<bool> AddOrUpdateUserInventoryAsync(ulong userId, ulong guildId, ObjectId itemId, int quantity)
        {
            var user = await GetUserAsync(userId).ConfigureAwait(false);
            // if exists update then return true
            if (user.Inventory.Any(x => x.Id == itemId))
            {
                var update = Builders<User>.Update.Set(x => x.Inventory[-1].Quantity, quantity);
                await UserCollection.UpdateOneAsync(x => x.Id == userId, update).ConfigureAwait(false);
                return true;
            }

            var newItem = Builders<User>.Update.Push(x => x.Inventory, new Item
            {
                Id = itemId,
                GuildId = guildId,
                Quantity = quantity
            });

            await UserCollection.UpdateOneAsync(x => x.Id == userId, newItem).ConfigureAwait(false);
            return false;
        }

        public async Task<Channel> GetOrAddChannelAsync(ITextChannel channel)
        {
            var dbChannel = await ChannelCollection.Find(c => c.Id == channel.Id).FirstOrDefaultAsync().ConfigureAwait(false);
            if (dbChannel != null)
            {
                return dbChannel;
            }

            dbChannel = new Channel
            {
                Id = channel.Id,
                Name = channel.Name,
                GuildId = channel.GuildId,
                IsNsfw = channel.IsNsfw,
            };

            await ChannelCollection.InsertOneAsync(dbChannel);
            return dbChannel;
        }

        public async Task DeleteChannelAsync(ITextChannel channel)
        {
            var updateChannel = Builders<Channel>.Update.Set(c => c.IsDeleted, true);
                
            await ChannelCollection.FindOneAndUpdateAsync(c => c.Id == channel.Id, updateChannel).ConfigureAwait(false);
        }

        public async Task UpdateChannelAsync(ITextChannel after)
        {
            var updateChannel = Builders<Channel>.Update.Set(c => c.Name, after.Name)
                .Set(c => c.GuildId, after.Guild.Id)
                .Set(c => c.IsNsfw, after.IsNsfw);
                
            await ChannelCollection.FindOneAndUpdateAsync(c => c.Id == after.Id, updateChannel).ConfigureAwait(false);
        }

        public bool IsLoggingEnabled(ITextChannel channel)
        {
            return ChannelCollection.Find(x => x.Id == channel.Id).First().Logging;
        }

        public async Task<Guild> GetOrAddGuildAsync(SocketGuild guild)
        {
            var dbGuild = await GuildCollection.Find(x => x.Id == guild.Id).FirstOrDefaultAsync().ConfigureAwait(false);
            if (dbGuild != null)
            {
                if (dbGuild.Available)
                    return dbGuild;
                
                await ChangeGuildAvailabilityAsync(guild, true).ConfigureAwait(false);
                return dbGuild;
            }

            dbGuild = new Guild
            {
                Id = guild.Id,
                IconId = guild.IconId,
                OwnerId = guild.OwnerId,
                ChannelCount = guild.Channels.Count,
                MemberCount = guild.MemberCount,
                EmoteCount = guild.Emotes.Count,
                Name = guild.Name,
                CreatedAt = guild.CreatedAt,
                RegionId = guild.VoiceRegionId
            };

            await GuildCollection.InsertOneAsync(dbGuild);
            return dbGuild;
        }

        public async Task<Guild> GetGuildAsync(ulong guildId)
        {
            return await GuildCollection.Find(g => g.Id == guildId).FirstAsync().ConfigureAwait(false);
        }

        public async Task ChangeGuildAvailabilityAsync(SocketGuild guild, bool available)
        {
            var update = Builders<Guild>.Update.Set(x => x.Available, available);
                
            await GuildCollection.FindOneAndUpdateAsync(x => x.Id == guild.Id, update).ConfigureAwait(false);
        }

        public async Task UpdateGuildAsync(SocketGuild after)
        {
            var updateGuild = Builders<Guild>.Update.Set(g => g.Name, after.Name)
                .Set(g => g.IconId, after.IconId)
                .Set(g => g.ChannelCount, after.Channels.Count)
                .Set(g => g.MemberCount, after.MemberCount)
                .Set(g => g.EmoteCount, after.Emotes.Count)
                .Set(g => g.OwnerId, after.OwnerId)
                .Set(g => g.RegionId, after.VoiceRegionId);

            await GuildCollection.FindOneAndUpdateAsync(g => g.Id == after.Id, updateGuild).ConfigureAwait(false);
        }

        public async Task AddStoreItemAsync(ulong guildId, Listing item)
        {
            var update = Builders<Guild>.Update.Push(g => g.Store, item);
            await GuildCollection.FindOneAndUpdateAsync(g => g.Id == guildId, update).ConfigureAwait(false);
        }

        public async Task<Listing> GetStoreItemByNameAsync(ulong guildId, string name)
        {
            var guild = await GetGuildAsync(guildId).ConfigureAwait(false);
            return guild.Store.FirstOrDefault(l => l.Name == name);
        }

        public async Task<Listing> GetStoreItemByIdAsync(ulong guildId, ObjectId id)
        {
            var guild = await GetGuildAsync(guildId).ConfigureAwait(false);
            return guild.Store.FirstOrDefault(l => l.Id == id);
        }

        public async Task<List<Listing>> GetStoreCatalogueAsync(ulong guildId)
        {
            var guild = await GetGuildAsync(guildId).ConfigureAwait(false);
            return guild.Store;
        }

        public async Task UpdateStoreItemAsync(ulong guildId, string name, int amount)
        {
            var update = Builders<Guild>.Update.Inc(g => g.Store[-1].Quantity, amount);
            await GuildCollection.FindOneAndUpdateAsync(g => g.Id == guildId && g.Store.Any(l => l.Name == name), update).ConfigureAwait(false);
        }

        public async Task AddMessageAsync(SocketMessage message)
        {
            await MessageCollection.InsertOneAsync(new Message
            {
                Id = message.Id,
                AuthorId = message.Author.Id,
                ChannelId = message.Channel.Id,
                GuildId = message.Channel is ITextChannel channelId ? channelId.GuildId : (ulong?) null,
                Content = message.Content,
                Attachments = message.Attachments?.Select(a => a.Url).ToList(),
                Timestamp = message.Timestamp.DateTime
            }).ConfigureAwait(false);
        }

        public async Task AddMessageEditAsync(SocketMessage after)
        {
            var update = Builders<Message>.Update.Push(m => m.Edits, new Edit
            {
                Content = after.Content,
                Attachments = after.Attachments.Select(m => m.Url).ToList(),
                EditedTimestamp = after.EditedTimestamp?.DateTime ?? after.Timestamp.DateTime
            });

            await MessageCollection.FindOneAndUpdateAsync(m => m.Id == after.Id, update).ConfigureAwait(false);
        }

        public async Task MessageDeletedAsync(ulong messageId)
        {
            var update = Builders<Message>.Update.Set(m => m.IsDeleted, true);

            await MessageCollection.FindOneAndUpdateAsync(m => m.Id == messageId, update).ConfigureAwait(false);
        }

        public async Task AddTransaction(Transaction transaction)
        {
            await TransactionCollection.InsertOneAsync(transaction).ConfigureAwait(false);
        }

        public IEnumerable<Transaction> GetTransactions(ulong userId, int page)
        {
            return TransactionCollection.AsQueryable().Where(t => t.From == userId || t.To == userId)
                .OrderByDescending(t => t.Id)
                .Skip(15 * page)
                .Take(15);
        }

        public Dictionary<string, List<JClue>> GetRandomJeopardyCategories(int number)
        {
            var query = JeopardyCollection.AsQueryable();
            var categories = query.Sample(number * 5).Select(x => x.Category).ToHashSet();
            while (categories.Count < number)
            {
                categories = query.Sample(number * 5).Select(x => x.Category).ToHashSet();
            }

            return categories.Take(number)
                .ToDictionary(category => category, category => new List<JClue>
                {
                    query.Where(x => x.Category == category && (x.Value == 200 || x.Value == 400))
                        .Sample(1)
                        .Select(x => new JClue {Category = category, Clue = x.Clue, Answer = x.Answer, Value = 200})
                        .First(),
                    query.Where(x => x.Category == category && (x.Value == 400 || x.Value == 800))
                        .Sample(1)
                        .Select(x => new JClue {Category = category, Clue = x.Clue, Answer = x.Answer, Value = 200})
                        .First(),
                    query.Where(x => x.Category == category && (x.Value == 600 || x.Value == 1200))
                        .Sample(1)
                        .Select(x => new JClue {Category = category, Clue = x.Clue, Answer = x.Answer, Value = 200})
                        .First(),
                    query.Where(x => x.Category == category && (x.Value == 800 || x.Value == 1600))
                        .Sample(1)
                        .Select(x => new JClue {Category = category, Clue = x.Clue, Answer = x.Answer, Value = 200})
                        .First(),
                    query.Where(x => x.Category == category && (x.Value == 1000 || x.Value == 2000))
                        .Sample(1)
                        .Select(x => new JClue {Category = category, Clue = x.Clue, Answer = x.Answer, Value = 200})
                        .First(),
                });
        }
    }
}