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
        Task<IAsyncCursor<User>> GetAllUserCursorAsync();
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
        Task ChargeInterestAsync(User user, decimal amount, string symbol);
        Task<bool> UpdateUserInvestingAccountAsync(User user, decimal amount);
        Task<bool> UpdateUserPortfolioAsync(User user, string symbol, string position, string action, long shares);

        Task<Quote> GetRandomQuoteAsync(ulong guildId, string keyword);
        Task<Quote> GetRandomQuoteAsync(ulong guildId, short id);
        Task AddQuoteAsync(Quote quote);
        Task<DeleteResult> DeleteQuoteAdmin(ulong guildId, short id = 0, string keyword = null);
        Task<DeleteResult> DeleteQuoteAsync(ulong guildId, ulong userId, short id);
        Task<DeleteResult> DeleteQuoteAsync(ulong guildId, ulong userId, string keyword);
        Task<List<Quote>> ListQuotesAsync(ulong guildId, int page);
        Task<List<Quote>> SearchQuotesByText(ulong guildId, string content);

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

        Task AddTrade(Trade trade);
        Task AddTransaction(Transaction transaction);
        IEnumerable<Transaction> GetTransactions(ulong userId, int page);

        Task<Dictionary<string, List<JClue>>> GetRandomJeopardyCategoriesAsync(int number);
        Task<JClue> GetFinalJeopardyAsync();

        Task<Event> GetEventByIdAsync(ObjectId id);
        Task AddNewEventAsync(Event e);
        Task<List<Event>> GetActiveEventsAsync();
        Task<List<Event>> GetActiveEventsByHostAsync(ulong hostId);
        Task<List<Event>> GetActiveEventsByGuild(ulong guildId);
        Task UpdateEventAsync(ObjectId id, UpdateDefinition<Event> update);

        Task<Pokemon> GetPokemonByNumberAsync(int id);
        Task<Pokemon> GetPokemonAsync(string name);
        Task<Ability> GetAbilityAsync(string name);
        Task<PokemonItem> GetItemAsync(string name);
        Task<Move> GetMoveAsync(string name);
    }

    public class MongoContext : IMongoContext
    {
        private readonly IMongoDatabase _database;
        
        public IMongoCollection<Message> MessageCollection { get; }
        public IMongoCollection<Channel> ChannelCollection { get; }
        public IMongoCollection<Guild> GuildCollection { get; }
        public IMongoCollection<User> UserCollection { get; }
        public IMongoCollection<Quote> QuoteCollection { get; }
        public IMongoCollection<Transaction> TransactionCollection { get; }
        public IMongoCollection<Trade> TradeCollection { get; }
        public IMongoCollection<JeopardyClue> JeopardyCollection { get; }
        public IMongoCollection<Event> EventCollection { get; }
        public IMongoCollection<Pokemon> PokedexCollection { get; }
        public IMongoCollection<Ability> AbilityCollection { get; }
        public IMongoCollection<PokemonItem> ItemCollection { get; }
        public IMongoCollection<Move> MoveCollection { get; }

        public MongoContext(IMongoDatabase database)
        {
            _database = database;
            
            MessageCollection = database.GetCollection<Message>("messages");
            ChannelCollection = database.GetCollection<Channel>("channels");
            GuildCollection = database.GetCollection<Guild>("guilds");
            UserCollection = database.GetCollection<User>("users");
            QuoteCollection = database.GetCollection<Quote>("quotes");
            TransactionCollection = database.GetCollection<Transaction>("transactions");
            TradeCollection = database.GetCollection<Trade>("trades");
            JeopardyCollection = database.GetCollection<JeopardyClue>("jeopardy");
            EventCollection = database.GetCollection<Event>("events");
            PokedexCollection = database.GetCollection<Pokemon>("pokemon.pokedex");
            AbilityCollection = database.GetCollection<Ability>("pokemon.abilities");
            ItemCollection = database.GetCollection<PokemonItem>("pokemon.items");
            MoveCollection = database.GetCollection<Move>("pokemon.moves");
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

        public async Task<IAsyncCursor<User>> GetAllUserCursorAsync()
        {
            return await UserCollection.FindAsync(FilterDefinition<User>.Empty);
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

        public async Task ChargeInterestAsync(User user, decimal amount, string symbol)
        {
            var update = user.InvestingAccount > 0 
                ? Builders<User>.Update.Inc(x => x.InvestingAccount, -amount) 
                : Builders<User>.Update.Inc(x => x.Currency, -(long) amount);

            update.Set(x => x.Portfolio[-1].InterestDate, DateTime.UtcNow.AddDays(7).Date);

            await UserCollection.UpdateOneAsync(x => x.Id == user.Id && x.Portfolio.Any(y => y.Symbol == symbol), update).ConfigureAwait(false);
        }

        public async Task<bool> UpdateUserInvestingAccountAsync(User user, decimal amount)
        {
            if (user.InvestingAccount + amount < 0)
            {
                return false;
            }

            var update = Builders<User>.Update.Inc(x => x.InvestingAccount, amount);
            await UserCollection.UpdateOneAsync(x => x.Id == user.Id, update).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> UpdateUserPortfolioAsync(User user, string symbol, string position, string action, long shares)
        {
            var portfolio = user.Portfolio;
            DateTime? interestDate = null;
            if (position == "short")
                interestDate = DateTime.UtcNow.AddDays(7).Date;
            
            if (portfolio.All(i => i.Symbol != symbol))
            {
                var update = Builders<User>.Update.Push(x => x.Portfolio, new Investment
                {
                    Symbol = symbol,
                    Position = position,
                    Shares = shares,
                    InterestDate = interestDate
                });

                await UserCollection.UpdateOneAsync(x => x.Id == user.Id, update).ConfigureAwait(false);
            }
            else
            {
                var investment = portfolio.First(i => i.Symbol == symbol);
                var newShares = investment.Shares + shares;
                if (newShares < 0)
                    return false;
                
                if (newShares == 0)
                {
                    var update = Builders<User>.Update.PullFilter(x => x.Portfolio, y => y.Symbol == symbol);
                    await UserCollection.UpdateOneAsync(x => x.Id == user.Id, update);
                }
                else
                {
                    var update = Builders<User>.Update.Inc(x => x.Portfolio[-1].Shares, shares);
                    await UserCollection.UpdateOneAsync(x => x.Id == user.Id, update);
                }
            }

            return true;
        }

        public async Task<Quote> GetRandomQuoteAsync(ulong guildId, string keyword)
        {
            var quote = await QuoteCollection.AsQueryable().Sample(1).Where(x => x.GuildId == guildId && x.Keyword == keyword).FirstOrDefaultAsync();
            if (quote == null)
                return null;
            var update = Builders<Quote>.Update.Inc(x => x.UseCount, 1);
            return await QuoteCollection.FindOneAndUpdateAsync<Quote>(x => x.Id == quote.Id, update,
                new FindOneAndUpdateOptions<Quote> {ReturnDocument = ReturnDocument.After}).ConfigureAwait(false);
        }

        public async Task<Quote> GetRandomQuoteAsync(ulong guildId, short id)
        {
            var quote = await QuoteCollection.AsQueryable().Sample(1).Where(x => x.GuildId == guildId && x.Id.Pid == id).FirstOrDefaultAsync();
            if (quote == null)
                return null;
            var update = Builders<Quote>.Update.Inc(x => x.UseCount, 1);
            return await QuoteCollection.FindOneAndUpdateAsync<Quote>(x => x.Id == quote.Id, update,
                new FindOneAndUpdateOptions<Quote> {ReturnDocument = ReturnDocument.After}).ConfigureAwait(false);
        }

        public async Task AddQuoteAsync(Quote quote)
        {
            await QuoteCollection.InsertOneAsync(quote).ConfigureAwait(false);
        }

        public async Task<DeleteResult> DeleteQuoteAdmin(ulong guildId, short id = 0, string keyword = null)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return await QuoteCollection.DeleteOneAsync(x => x.GuildId == guildId && x.Keyword == keyword).ConfigureAwait(false);
            }
            else
            {
                return await QuoteCollection.DeleteOneAsync(x => x.GuildId == guildId && x.Id.Pid == id).ConfigureAwait(false);
            }
        }

        public async Task<DeleteResult> DeleteQuoteAsync(ulong guildId, ulong userId, short id)
        {
            return await QuoteCollection.DeleteOneAsync(x => x.GuildId == guildId && x.AuthorId == userId && x.Id.Pid == id).ConfigureAwait(false);
        }

        public async Task<DeleteResult> DeleteQuoteAsync(ulong guildId, ulong userId, string keyword)
        {
            return await QuoteCollection.DeleteOneAsync(x => x.GuildId == guildId && x.AuthorId == userId && x.Keyword == keyword).ConfigureAwait(false);
        }

        public async Task<List<Quote>> ListQuotesAsync(ulong guildId, int page)
        {
            return await QuoteCollection.Aggregate()
                .Match(x => x.GuildId == guildId)
                .SortBy(x => x.Keyword)
                .Skip(page * 15)
                .Limit(15)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<List<Quote>> SearchQuotesByText(ulong guildId, string content)
        {
            var filter = Builders<Quote>.Filter.Text(content);
            return await QuoteCollection.Find(filter)
                .Limit(9)
                .ToListAsync()
                .ConfigureAwait(false);
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

        public async Task AddTrade(Trade trade)
        {
            await TradeCollection.InsertOneAsync(trade).ConfigureAwait(false);
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

        public async Task<Dictionary<string, List<JClue>>> GetRandomJeopardyCategoriesAsync(int number)
        {
            var query = JeopardyCollection.AsQueryable();
            var categories = query.Sample(number * 5).Select(x => x.Category).ToHashSet();
            while (categories.Count < number)
            {
                categories = query.Sample(number * 5).Select(x => x.Category).ToHashSet();
            }

            var result = new Dictionary<string, List<JClue>>();
            foreach (var category in categories.TakeWhile(category => result.Count != number))
            {
                try
                {
                    var all = await JeopardyCollection.Find(x => x.Category == category).ToListAsync();

                    result.Add(category, new List<JClue>
                    {
                        all.Where(x => x.Value == 200).Take(1)
                            .Select(x => new JClue {Answer = x.Answer, Category = x.Category, Clue = x.Category, Value = 200}).Single(),
                        all.Where(x => x.Value == 400).Take(1)
                            .Select(x => new JClue {Answer = x.Answer, Category = x.Category, Clue = x.Category, Value = 400}).Single(),
                        all.Where(x => x.Value == 600 || x.Value == 1200).Take(1)
                            .Select(x => new JClue {Answer = x.Answer, Category = x.Category, Clue = x.Category, Value = 600}).Single(),
                        all.Where(x => x.Value == 800 || x.Value == 1600).Take(1)
                            .Select(x => new JClue {Answer = x.Answer, Category = x.Category, Clue = x.Category, Value = 800}).Single(),
                        all.Where(x => x.Value == 200 || x.Value == 2000).Take(1)
                            .Select(x => new JClue {Answer = x.Answer, Category = x.Category, Clue = x.Category, Value = 1000}).Single(),
                    });
                }
                catch (Exception)
                {
                    //
                }
            }
            
            return result;
        }

        public async Task<JClue> GetFinalJeopardyAsync()
        {
            var clue = await JeopardyCollection.AsQueryable().Sample(1).Where(x => x.Value > 2000 || x.Value == null).SingleAsync();

            return new JClue
            {
                Category = clue.Category,
                Clue = clue.Clue,
                Answer = clue.Answer,
                Value = 0
            };
        }

        public async Task<Event> GetEventByIdAsync(ObjectId id)
        {
            return await EventCollection.Find(x => x.Id == id).FirstAsync();
        }

        public async Task AddNewEventAsync(Event e)
        {
            await EventCollection.InsertOneAsync(e).ConfigureAwait(false);
        }

        public async Task<List<Event>> GetActiveEventsAsync()
        {
            var now = DateTime.UtcNow;
            return await EventCollection.Find(x => x.StartDate > now && !x.Deleted).ToListAsync();
        }

        public async Task<List<Event>> GetActiveEventsByHostAsync(ulong hostId)
        {
            var now = DateTime.UtcNow;
            return await EventCollection.Find(x => x.Host == hostId && x.StartDate > now && !x.Deleted).ToListAsync();
        }

        public async Task<List<Event>> GetActiveEventsByGuild(ulong guildId)
        {
            var now = DateTime.UtcNow;
            return await EventCollection.Find(x => x.GuildId == guildId && x.StartDate > now && !x.Deleted).ToListAsync();
        }

        public async Task UpdateEventAsync(ObjectId id, UpdateDefinition<Event> update)
        {
            await EventCollection.UpdateOneAsync(x => x.Id == id, update).ConfigureAwait(false);
        }

        public async Task<Pokemon> GetPokemonByNumberAsync(int id)
        {
            return await PokedexCollection.Find(x => x.Num == id).FirstOrDefaultAsync();
        }

        public async Task<Pokemon> GetPokemonAsync(string name)
        {
            return await PokedexCollection.Find(x => x.Id == name).FirstOrDefaultAsync();
        }

        public async Task<Ability> GetAbilityAsync(string name)
        {
            return await AbilityCollection.Find(x => x.Id == name).FirstOrDefaultAsync();
        }

        public async Task<PokemonItem> GetItemAsync(string name)
        {
            return await ItemCollection.Find(x => x.Id == name).FirstOrDefaultAsync();
        }

        public async Task<Move> GetMoveAsync(string name)
        {
            return await MoveCollection.Find(x => x.Id == name).FirstOrDefaultAsync();
        }
    }
}