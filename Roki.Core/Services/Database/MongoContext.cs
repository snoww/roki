using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Roki.Extensions;
using Roki.Modules.Games.Common;
using Roki.Services.Database.Maps;

namespace Roki.Services.Database
{
    public interface IMongoContext
    {
        IMongoCollection<Message> MessageCollection { get; }
        IMongoCollection<Channel> ChannelCollection { get; }
        IMongoCollection<Guild> GuildCollection { get; }
        IMongoCollection<User> UserCollection { get; }
        IMongoCollection<Transaction> TransactionCollection { get; }
        IMongoCollection<JeopardyClue> JeopardyCollection { get; }

        #region Users

        Task<User> GetOrAddUserAsync(IUser user, string guildId);
        Task<IAsyncCursor<User>> GetAllUserCursorAsync();
        Task<User> UpdateUserAsync(IUser after);
        Task UpdateUserXpAsync(User user, string guildId, DateTime now, int increment, bool levelUp);
        Task<int> GetUserXpRankAsync(User user, string guildId);
        Task UpdateUserNotificationPreferenceAsync(ulong userId, string guildId, string location);
        Task<bool> UpdateUserCurrencyAsync(User user, string guildId, long amount);
        Task<bool> UpdateUserCurrencyAsync(IUser user, string guildId, long amount);
        Task UpdateBotCurrencyAsync(long amount, string guildId);
        Task<long> GetUserCurrency(IUser user, string guildId);
        Task<decimal> GetUserInvesting(IUser user, string guildId);
        Task<IEnumerable<User>> GetCurrencyLeaderboardAsync(string guildId, int page);
        Task<IEnumerable<User>> GetXpLeaderboardAsync(string guildId, int page);
        Task<bool> TransferCurrencyAsync(IUser user, string guildId, decimal amount);
        Task<bool> AddOrUpdateUserSubscriptionAsync(IUser user, string guildId, ObjectId subId, int days);
        Task<Dictionary<ulong, Dictionary<string, List<ObjectId>>>> RemoveExpiredSubscriptionsAsync();
        Task<bool> AddOrUpdateUserInventoryAsync(IUser user, string guildId, ObjectId itemId, int quantity);
        Task ChargeInterestAsync(User user, string guildId, decimal amount, string ticker);
        Task<bool> UpdateUserInvestingAccountAsync(User user, string guildId, decimal amount);
        Task<bool> UpdateUserPortfolioAsync(User user, string guildId, string ticker, string position, long shares);

        #endregion

        #region Quotes

        Task<Quote> GetRandomQuoteAsync(ulong guildId, string keyword);
        Task<Quote> GetQuoteByIdAsync(ulong guildId, string id);
        Task AddQuoteAsync(Quote quote);
        Task<DeleteResult> DeleteQuoteAdmin(ulong guildId, string id = null, string keyword = null);
        Task<DeleteResult> DeleteQuoteByIdAsync(ulong guildId, ulong userId, string id);
        Task<DeleteResult> DeleteQuoteAsync(ulong guildId, ulong userId, string keyword);
        Task<List<Quote>> ListQuotesAsync(ulong guildId, int page);
        Task<List<Quote>> SearchQuotesByText(ulong guildId, string content);

        #endregion

        #region Channels

        Task<Channel> GetOrAddChannelAsync(ITextChannel channel);
        Task DeleteChannelAsync(ITextChannel channel);
        Task UpdateChannelAsync(ITextChannel after);
        Task<ChannelConfig> GetChannelConfigAsync(ITextChannel channel);
        Task UpdateChannelConfigAsync(ITextChannel channel, ChannelConfig channelConfig);

        #endregion

        #region Guilds

        Task<Guild> GetOrAddGuildAsync(SocketGuild guild);
        Task<Guild> GetGuildAsync(ulong guildId);
        Task<GuildConfig> GetGuildConfigAsync(ulong guildId);
        Task UpdateGuildConfigAsync(ulong guildId, GuildConfig guildConfig);
        Task ChangeGuildAvailabilityAsync(SocketGuild guild, bool available);
        Task UpdateGuildAsync(SocketGuild after);
        Task AddXpRewardAsync(ulong guildId, ObjectId id, XpReward reward);
        Task<UpdateResult> RemoveXpRewardAsync(ulong guildId, ObjectId id);
        Task AddStoreItemAsync(ulong guildId, ObjectId id, Listing item);
        Task<(ObjectId, Listing)> GetStoreItemByNameAsync(ulong guildId, string name);
        Task<(ObjectId, Listing)> GetStoreItemByObjectIdAsync(ulong guildId, ObjectId id);
        Task<(ObjectId, Listing)> GetStoreItemByIdAsync(ulong guildId, string id);
        Task<Dictionary<ObjectId, Listing>> GetStoreCatalogueAsync(ulong guildId);
        Task UpdateStoreItemAsync(ulong guildId, ObjectId id, int amount);

        #endregion

        #region Messages

        Task AddMessageAsync(SocketMessage message);
        Task AddMessageEditAsync(SocketMessage after);
        Task MessageDeletedAsync(ulong messageId);

        #endregion

        #region Transactions

        Task AddTrade(Trade trade);
        Task AddTransaction(Transaction transaction);
        IEnumerable<Transaction> GetTransactions(ulong userId, int page);

        #endregion

        #region Jeopardy

        Task<Dictionary<string, List<JClue>>> GetRandomJeopardyCategoriesAsync(int number);
        Task<JClue> GetFinalJeopardyAsync();

        #endregion

        #region Events

        Task<Event> GetEventByIdAsync(ObjectId id);
        Task AddNewEventAsync(Event e);
        Task<List<Event>> GetActiveEventsAsync();
        Task<List<Event>> GetActiveEventsByHostAsync(ulong hostId);
        Task<List<Event>> GetActiveEventsByGuild(ulong guildId);
        bool GetActiveEventAsync(ulong messageId, out Event e);
        Task UpdateEventAsync(ObjectId id, UpdateDefinition<Event> update);

        #endregion

        #region Pokemon

        Task<Pokemon> GetPokemonByNumberAsync(int id);
        Task<Pokemon> GetPokemonAsync(string name);
        Task<Ability> GetAbilityAsync(string name);
        Task<PokemonItem> GetItemAsync(string name);
        Task<Move> GetMoveAsync(string name);

        #endregion
    }

    public class MongoContext : IMongoContext
    {
        private readonly IMongoDatabase _database;

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

        public IMongoCollection<Quote> QuoteCollection { get; }
        public IMongoCollection<Trade> TradeCollection { get; }
        public IMongoCollection<Event> EventCollection { get; }
        public IMongoCollection<Pokemon> PokedexCollection { get; }
        public IMongoCollection<Ability> AbilityCollection { get; }
        public IMongoCollection<PokemonItem> ItemCollection { get; }
        public IMongoCollection<Move> MoveCollection { get; }

        public IMongoCollection<Message> MessageCollection { get; }
        public IMongoCollection<Channel> ChannelCollection { get; }
        public IMongoCollection<Guild> GuildCollection { get; }
        public IMongoCollection<User> UserCollection { get; }
        public IMongoCollection<Transaction> TransactionCollection { get; }
        public IMongoCollection<JeopardyClue> JeopardyCollection { get; }

        public async Task<User> GetOrAddUserAsync(IUser user, string guildId)
        {
            User dbUser = await UserCollection.Find(u => u.Id == user.Id).FirstOrDefaultAsync().ConfigureAwait(false);
            if (dbUser != null)
            {
                if (!dbUser.Data.ContainsKey(guildId))
                {
                    UpdateDefinition<User> update = Builders<User>.Update.Set(x => x.Data[guildId], new UserData());
                    dbUser = await UserCollection.FindOneAndUpdateAsync<User>(x => x.Id == user.Id, update, new FindOneAndUpdateOptions<User> {ReturnDocument = ReturnDocument.After});
                }

                return dbUser;
            }

            dbUser = new User
            {
                Id = user.Id,
                Discriminator = user.Discriminator,
                AvatarId = user.AvatarId,
                Username = user.Username,
                Data = {[guildId] = new UserData()}
            };

            await UserCollection.InsertOneAsync(dbUser);
            return dbUser;
        }

        public async Task<IAsyncCursor<User>> GetAllUserCursorAsync()
        {
            return await UserCollection.FindAsync(FilterDefinition<User>.Empty);
        }

        public async Task<User> UpdateUserAsync(IUser after)
        {
            UpdateDefinition<User> updateUser = Builders<User>.Update.Set(u => u.Id, after.Id)
                .Set(u => u.Username, after.Username)
                .Set(u => u.Discriminator, after.Discriminator)
                .Set(u => u.AvatarId, after.AvatarId);

            return await UserCollection.FindOneAndUpdateAsync<User>(u => u.Id == after.Id, updateUser,
                new FindOneAndUpdateOptions<User> {ReturnDocument = ReturnDocument.After, IsUpsert = true}).ConfigureAwait(false);
        }

        public async Task UpdateUserXpAsync(User user, string guildId, DateTime now, int increment, bool levelUp)
        {
            if (levelUp)
            {
                UpdateDefinition<User> updateXp = Builders<User>.Update.Inc(u => u.Data[guildId].Xp, increment)
                    .Set(u => u.Data[guildId].LastLevelUp, now)
                    .Set(u => u.Data[guildId].LastXpGain, now);
                await UserCollection.UpdateOneAsync(u => u.Id == user.Id, updateXp).ConfigureAwait(false);
            }
            else
            {
                UpdateDefinition<User> updateXp = Builders<User>.Update.Inc(u => u.Data[guildId].Xp, increment)
                    .Set(u => u.Data[guildId].LastXpGain, now);
                await UserCollection.UpdateOneAsync(u => u.Id == user.Id, updateXp).ConfigureAwait(false);
            }
        }

        public async Task<int> GetUserXpRankAsync(User user, string guildId)
        {
            return (int) await UserCollection.CountDocumentsAsync(x => x.Data[guildId].Xp > user.Data[guildId].Xp).ConfigureAwait(false) + 1;
        }

        public async Task UpdateUserNotificationPreferenceAsync(ulong userId, string guildId, string location)
        {
            UpdateDefinition<User> update = Builders<User>.Update.Set(x => x.Data[guildId].Notification, location);
            await UserCollection.UpdateOneAsync(x => x.Id == userId, update).ConfigureAwait(false);
        }

        public async Task<bool> UpdateUserCurrencyAsync(User user, string guildId, long amount)
        {
            if (user.Data[guildId].Currency + amount < 0)
            {
                return false;
            }

            UpdateDefinition<User> updateCurrency = Builders<User>.Update.Inc(u => u.Data[guildId].Currency, amount);
            await UserCollection.UpdateOneAsync(u => u.Id == user.Id, updateCurrency).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> UpdateUserCurrencyAsync(IUser user, string guildId, long amount)
        {
            User dbUser = await GetOrAddUserAsync(user, guildId).ConfigureAwait(false);
            return await UpdateUserCurrencyAsync(dbUser, guildId, amount).ConfigureAwait(false);
        }

        public async Task UpdateBotCurrencyAsync(long amount, string guildId)
        {
            UpdateDefinition<User> updateCurrency = Builders<User>.Update.Inc(u => u.Data[guildId].Currency, amount);
            // todo get bot id
            await UserCollection.UpdateOneAsync(u => u.Id == Roki.BotId, updateCurrency).ConfigureAwait(false);
        }

        public async Task<long> GetUserCurrency(IUser user, string guildId)
        {
            return (await GetOrAddUserAsync(user, guildId)).Data[guildId].Currency;
        }

        public async Task<decimal> GetUserInvesting(IUser user, string guildId)
        {
            return (await GetOrAddUserAsync(user, guildId)).Data[guildId].InvestingAccount;
        }

        public async Task<IEnumerable<User>> GetCurrencyLeaderboardAsync(string guildId, int page)
        {
            return await UserCollection.Aggregate()
                // todo get botid
                .Match(u => u.Data[guildId].Currency > 0 && u.Id != Roki.BotId)
                .SortByDescending(u => u.Data[guildId].Currency)
                .Skip(page * 9)
                .Limit(9)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<User>> GetXpLeaderboardAsync(string guildId, int page)
        {
            return await UserCollection.Aggregate()
                .Match(u => u.Data[guildId].Xp > 0 && u.Id != Roki.BotId)
                .SortByDescending(u => u.Data[guildId].Xp)
                .Skip(page * 9)
                .Limit(9)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<bool> TransferCurrencyAsync(IUser user, string guildId, decimal amount)
        {
            User dbUser = await GetOrAddUserAsync(user, guildId).ConfigureAwait(false);
            long cash = dbUser.Data[guildId].Currency;
            decimal invest = dbUser.Data[guildId].InvestingAccount;

            if ((amount <= 0 || cash - amount < 0) && (amount >= 0 || invest + amount < 0))
            {
                return false;
            }

            UpdateDefinition<User> update = Builders<User>.Update.Inc(u => u.Data[guildId].Currency, -(long) amount)
                .Inc(u => u.Data[guildId].InvestingAccount, amount);

            await UserCollection.UpdateOneAsync(u => u.Id == user.Id, update).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> AddOrUpdateUserSubscriptionAsync(IUser user, string guildId, ObjectId subId, int days)
        {
            User dbUser = await GetOrAddUserAsync(user, guildId).ConfigureAwait(false);
            // if exists update then return true
            Subscription sub = dbUser.Data[guildId].Subscriptions[subId];
            if (sub != null)
            {
                UpdateDefinition<User> update = Builders<User>.Update.Set(x => x.Data[guildId].Subscriptions[subId].EndDate, sub.EndDate.AddDays(days).Date);
                await UserCollection.UpdateOneAsync(x => x.Id == user.Id, update).ConfigureAwait(false);
                return true;
            }

            UpdateDefinition<User> newSub = Builders<User>.Update.Set(x => x.Data[guildId].Subscriptions[subId], new Subscription
            {
                EndDate = DateTime.UtcNow.AddDays(days).Date
            });

            await UserCollection.UpdateOneAsync(x => x.Id == user.Id, newSub).ConfigureAwait(false);
            return false;
        }

        public async Task<Dictionary<ulong, Dictionary<string, List<ObjectId>>>> RemoveExpiredSubscriptionsAsync()
        {
            DateTime now = DateTime.UtcNow.Date;
            var expired = new Dictionary<ulong, Dictionary<string, List<ObjectId>>>();

            using IAsyncCursor<User> cursor = await UserCollection.FindAsync(FilterDefinition<User>.Empty);
            while (await cursor.MoveNextAsync())
            {
                foreach (User user in cursor.Current)
                {
                    expired[user.Id] = new Dictionary<string, List<ObjectId>>();
                    foreach ((string guildId, UserData data) in user.Data)
                    {
                        expired[user.Id][guildId] = new List<ObjectId>();
                        foreach ((ObjectId id, Subscription subscription) in data.Subscriptions)
                        {
                            if (now >= subscription.EndDate)
                            {
                                expired[user.Id][guildId].Add(id);
                                UpdateDefinition<User> remove = Builders<User>.Update.Unset(x => x.Data[guildId].Subscriptions[id]);
                                await UserCollection.UpdateOneAsync(x => x.Id == user.Id, remove);
                            }
                        }

                        if (expired[user.Id][guildId].Count == 0)
                        {
                            expired[user.Id].Remove(guildId);
                        }
                    }

                    if (expired[user.Id].Count == 0)
                    {
                        expired.Remove(user.Id);
                    }
                }
            }

            return expired;
        }

        public async Task<bool> AddOrUpdateUserInventoryAsync(IUser user, string guildId, ObjectId itemId, int quantity)
        {
            User dbUser = await GetOrAddUserAsync(user, guildId).ConfigureAwait(false);
            // if exists update then return true
            if (dbUser.Data[guildId].Inventory.ContainsKey(itemId))
            {
                UpdateDefinition<User> update = Builders<User>.Update.Inc(x => x.Data[guildId].Inventory[itemId].Quantity, quantity);
                await UserCollection.UpdateOneAsync(x => x.Id == user.Id, update).ConfigureAwait(false);
                return true;
            }

            UpdateDefinition<User> newItem = Builders<User>.Update.Set(x => x.Data[guildId].Inventory[itemId], new Item
            {
                Quantity = quantity
            });

            await UserCollection.UpdateOneAsync(x => x.Id == user.Id, newItem).ConfigureAwait(false);
            return false;
        }

        public async Task ChargeInterestAsync(User user, string guildId, decimal amount, string ticker)
        {
            UpdateDefinition<User> update = user.Data[guildId].InvestingAccount > 0
                ? Builders<User>.Update.Inc(x => x.Data[guildId].InvestingAccount, -amount)
                : Builders<User>.Update.Inc(x => x.Data[guildId].Currency, -(long) amount);

            // todo maybe stop charging interest after they are like -100k or something idk 

            update.Set(x => x.Data[guildId].Portfolio[ticker].InterestDate, DateTime.UtcNow.AddDays(7).Date);

            await UserCollection.UpdateOneAsync(x => x.Id == user.Id, update).ConfigureAwait(false);
        }

        public async Task<bool> UpdateUserInvestingAccountAsync(User user, string guildId, decimal amount)
        {
            if (user.Data[guildId].InvestingAccount + amount < 0)
            {
                return false;
            }

            UpdateDefinition<User> update = Builders<User>.Update.Inc(x => x.Data[guildId].InvestingAccount, amount);
            await UserCollection.UpdateOneAsync(x => x.Id == user.Id, update).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> UpdateUserPortfolioAsync(User user, string guildId, string ticker, string position, long shares)
        {
            Dictionary<string, Investment> portfolio = user.Data[guildId].Portfolio;
            DateTime? interestDate = null;
            if (position == "short")
            {
                interestDate = DateTime.UtcNow.AddDays(7).Date;
            }

            if (portfolio.ContainsKey(ticker))
            {
                long newShares = portfolio[ticker].Shares + shares;
                if (newShares < 0)
                {
                    return false;
                }

                if (newShares == 0)
                {
                    UpdateDefinition<User> update = Builders<User>.Update.Unset(x => x.Data[guildId].Portfolio[ticker]);
                    await UserCollection.UpdateOneAsync(x => x.Id == user.Id, update);
                }
                else
                {
                    UpdateDefinition<User> update = Builders<User>.Update.Inc(x => x.Data[guildId].Portfolio[ticker].Shares, shares);
                    await UserCollection.UpdateOneAsync(x => x.Id == user.Id, update);
                }
            }
            else
            {
                UpdateDefinition<User> update = Builders<User>.Update.Set(x => x.Data[guildId].Portfolio[ticker], new Investment
                {
                    Position = position,
                    Shares = shares,
                    InterestDate = interestDate
                });

                await UserCollection.UpdateOneAsync(x => x.Id == user.Id, update).ConfigureAwait(false);
            }

            return true;
        }

        public async Task<Quote> GetRandomQuoteAsync(ulong guildId, string keyword)
        {
            Quote quote = await QuoteCollection.AsQueryable().Where(x => x.GuildId == guildId && x.Keyword == keyword).Sample(1).SingleOrDefaultAsync();
            if (quote == null)
            {
                return null;
            }

            UpdateDefinition<Quote> update = Builders<Quote>.Update.Inc(x => x.UseCount, 1);
            return await QuoteCollection.FindOneAndUpdateAsync<Quote>(x => x.Id == quote.Id, update,
                new FindOneAndUpdateOptions<Quote> {ReturnDocument = ReturnDocument.After}).ConfigureAwait(false);
        }

        public async Task<Quote> GetQuoteByIdAsync(ulong guildId, string id)
        {
            id = id.ToLowerInvariant();
            Quote quote = await QuoteCollection.AsQueryable().Where(x => x.GuildId == guildId && x.Id.ToString().Substring(18) == id).Sample(1).SingleOrDefaultAsync();
            if (quote == null)
            {
                return null;
            }

            UpdateDefinition<Quote> update = Builders<Quote>.Update.Inc(x => x.UseCount, 1);
            return await QuoteCollection.FindOneAndUpdateAsync<Quote>(x => x.Id == quote.Id, update,
                new FindOneAndUpdateOptions<Quote> {ReturnDocument = ReturnDocument.After}).ConfigureAwait(false);
        }

        public async Task AddQuoteAsync(Quote quote)
        {
            await QuoteCollection.InsertOneAsync(quote).ConfigureAwait(false);
        }

        public async Task<DeleteResult> DeleteQuoteAdmin(ulong guildId, string id = null, string keyword = null)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                id = id.ToLowerInvariant();
                return await QuoteCollection.DeleteOneAsync(x => x.GuildId == guildId && x.Id.ToString().Substring(18) == id).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                return await QuoteCollection.DeleteOneAsync(x => x.GuildId == guildId && x.Keyword == keyword).ConfigureAwait(false);
            }

            return await QuoteCollection.DeleteOneAsync(x => x.GuildId == guildId && x.Id.ToString().Substring(18) == id.ToLowerInvariant() && x.Keyword == keyword).ConfigureAwait(false);
        }

        public async Task<DeleteResult> DeleteQuoteByIdAsync(ulong guildId, ulong userId, string id)
        {
            id = id.ToLowerInvariant();
            return await QuoteCollection.DeleteOneAsync(x => x.GuildId == guildId && x.AuthorId == userId && x.Id.ToString().Substring(18) == id).ConfigureAwait(false);
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
            FilterDefinition<Quote> filter = Builders<Quote>.Filter.Text(content);
            filter &= Builders<Quote>.Filter.Eq(x => x.GuildId, guildId);
            return await QuoteCollection.Find(filter)
                .Limit(9)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<Channel> GetOrAddChannelAsync(ITextChannel channel)
        {
            Channel dbChannel = await ChannelCollection.Find(c => c.Id == channel.Id).FirstOrDefaultAsync().ConfigureAwait(false);
            if (dbChannel != null)
            {
                return dbChannel;
            }

            GuildConfig guildConfig = await GetGuildConfigAsync(channel.GuildId);
            dbChannel = new Channel
            {
                Id = channel.Id,
                Name = channel.Name,
                GuildId = channel.GuildId,
                IsNsfw = channel.IsNsfw,
                CreatedAt = channel.CreatedAt,
                Config = new ChannelConfig
                {
                    Logging = guildConfig.Logging,
                    CurrencyGeneration = guildConfig.CurrencyGeneration,
                    XpGain = guildConfig.XpGain,
                    Modules = guildConfig.Modules,
                    Commands = guildConfig.Commands
                }
            };

            await ChannelCollection.InsertOneAsync(dbChannel);
            return dbChannel;
        }

        public async Task DeleteChannelAsync(ITextChannel channel)
        {
            UpdateDefinition<Channel> updateChannel = Builders<Channel>.Update.Set(c => c.IsDeleted, true);

            await ChannelCollection.FindOneAndUpdateAsync(c => c.Id == channel.Id, updateChannel).ConfigureAwait(false);
        }

        public async Task UpdateChannelAsync(ITextChannel after)
        {
            UpdateDefinition<Channel> updateChannel = Builders<Channel>.Update.Set(c => c.Name, after.Name)
                .Set(c => c.GuildId, after.Guild.Id)
                .Set(c => c.IsNsfw, after.IsNsfw);

            await ChannelCollection.FindOneAndUpdateAsync(c => c.Id == after.Id, updateChannel).ConfigureAwait(false);
        }

        public async Task<ChannelConfig> GetChannelConfigAsync(ITextChannel channel)
        {
            return (await GetOrAddChannelAsync(channel).ConfigureAwait(false)).Config;
        }

        public async Task UpdateChannelConfigAsync(ITextChannel channel, ChannelConfig channelConfig)
        {
            await ChannelCollection.UpdateOneAsync(x => x.Id == channel.Id, Builders<Channel>.Update.Set(x => x.Config, channelConfig));
        }

        public async Task<Guild> GetOrAddGuildAsync(SocketGuild guild)
        {
            Guild dbGuild = await GuildCollection.Find(x => x.Id == guild.Id).FirstOrDefaultAsync().ConfigureAwait(false);
            if (dbGuild != null)
            {
                if (dbGuild.Available)
                {
                    return dbGuild;
                }

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

        public async Task<GuildConfig> GetGuildConfigAsync(ulong guildId)
        {
            return (await GuildCollection.Find(g => g.Id == guildId).FirstAsync().ConfigureAwait(false)).Config;
        }

        public async Task UpdateGuildConfigAsync(ulong guildId, GuildConfig guildConfig)
        {
            await GuildCollection.UpdateOneAsync(x => x.Id == guildId, Builders<Guild>.Update.Set(x => x.Config, guildConfig));
        }

        public async Task ChangeGuildAvailabilityAsync(SocketGuild guild, bool available)
        {
            await GuildCollection.UpdateOneAsync(x => x.Id == guild.Id, Builders<Guild>.Update.Set(x => x.Available, available)).ConfigureAwait(false);
        }

        public async Task UpdateGuildAsync(SocketGuild after)
        {
            UpdateDefinition<Guild> updateGuild = Builders<Guild>.Update.Set(g => g.Name, after.Name)
                .Set(g => g.IconId, after.IconId)
                .Set(g => g.ChannelCount, after.Channels.Count)
                .Set(g => g.MemberCount, after.MemberCount)
                .Set(g => g.EmoteCount, after.Emotes.Count)
                .Set(g => g.OwnerId, after.OwnerId)
                .Set(g => g.RegionId, after.VoiceRegionId);

            await GuildCollection.UpdateOneAsync(g => g.Id == after.Id, updateGuild).ConfigureAwait(false);
        }

        public async Task AddXpRewardAsync(ulong guildId, ObjectId id, XpReward reward)
        {
            UpdateDefinition<Guild> update = Builders<Guild>.Update.Set(x => x.XpRewards[id], reward);
            await GuildCollection.UpdateOneAsync(x => x.Id == guildId, update).ConfigureAwait(false);
        }

        public async Task<UpdateResult> RemoveXpRewardAsync(ulong guildId, ObjectId id)
        {
            UpdateDefinition<Guild> update = Builders<Guild>.Update.Unset(x => x.XpRewards[id]);
            return await GuildCollection.UpdateOneAsync(x => x.Id == guildId, update).ConfigureAwait(false);
        }

        public async Task AddStoreItemAsync(ulong guildId, ObjectId id, Listing item)
        {
            UpdateDefinition<Guild> update = Builders<Guild>.Update.Set(g => g.Store[id], item);
            await GuildCollection.UpdateOneAsync(g => g.Id == guildId, update).ConfigureAwait(false);
        }

        public async Task<(ObjectId, Listing)> GetStoreItemByNameAsync(ulong guildId, string name)
        {
            Guild guild = await GetGuildAsync(guildId).ConfigureAwait(false);
            (ObjectId key, Listing value) = guild.Store.FirstOrDefault(l => l.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return (key, value);
        }

        public async Task<(ObjectId, Listing)> GetStoreItemByObjectIdAsync(ulong guildId, ObjectId id)
        {
            Guild guild = await GetGuildAsync(guildId).ConfigureAwait(false);
            (ObjectId key, Listing value) = guild.Store.FirstOrDefault(l => l.Key == id);
            return (key, value);
        }

        public async Task<(ObjectId, Listing)> GetStoreItemByIdAsync(ulong guildId, string id)
        {
            id = id.ToLowerInvariant();
            Guild guild = await GetGuildAsync(guildId).ConfigureAwait(false);
            (ObjectId key, Listing value) = guild.Store.FirstOrDefault(l => l.Key.GetHexId() == id);
            return (key, value);
        }

        public async Task<Dictionary<ObjectId, Listing>> GetStoreCatalogueAsync(ulong guildId)
        {
            Guild guild = await GetGuildAsync(guildId).ConfigureAwait(false);
            return guild.Store;
        }

        public async Task UpdateStoreItemAsync(ulong guildId, ObjectId id, int amount)
        {
            UpdateDefinition<Guild> update = Builders<Guild>.Update.Inc(g => g.Store[id].Quantity, amount);
            await GuildCollection.UpdateOneAsync(g => g.Id == guildId, update).ConfigureAwait(false);
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
                MessageReference = message.Reference?.MessageId.GetValueOrDefault(),
                Attachments = message.Attachments?.Select(a => a.Url).ToList(),
                Timestamp = message.Timestamp.DateTime
            }).ConfigureAwait(false);
        }

        public async Task AddMessageEditAsync(SocketMessage after)
        {
            UpdateDefinition<Message> update = Builders<Message>.Update.Push(m => m.Edits, new Edit
            {
                Content = after.Content,
                Attachments = after.Attachments.Select(m => m.Url).ToList(),
                EditedTimestamp = after.EditedTimestamp?.DateTime ?? after.Timestamp.DateTime
            });

            await MessageCollection.UpdateOneAsync(m => m.Id == after.Id, update).ConfigureAwait(false);
        }

        public async Task MessageDeletedAsync(ulong messageId)
        {
            UpdateDefinition<Message> update = Builders<Message>.Update.Set(m => m.IsDeleted, true);

            await MessageCollection.UpdateOneAsync(m => m.Id == messageId, update).ConfigureAwait(false);
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
            IMongoQueryable<JeopardyClue> query = JeopardyCollection.AsQueryable();
            HashSet<string> categories = query.Sample(number * 5).Select(x => x.Category).ToHashSet();
            while (categories.Count < number)
            {
                categories = query.Sample(number * 5).Select(x => x.Category).ToHashSet();
            }

            var result = new Dictionary<string, List<JClue>>();
            foreach (string category in categories.TakeWhile(category => result.Count != number))
            {
                try
                {
                    List<JeopardyClue> all = await JeopardyCollection.Find(x => x.Category == category).ToListAsync();

                    JClue two = all.Where(x => x.Value == 200).Take(1).Select(x => new JClue(x.Answer, x.Category, x.Clue, 200)).Single();
                    JClue four = all.Where(x => x.Value == 400).Take(1).Select(x => new JClue(x.Answer, x.Category, x.Clue, 400)).Single();
                    JClue six = all.Where(x => x.Value == 600 || x.Value == 1200).Take(1).Select(x => new JClue(x.Answer, x.Category, x.Clue, 600)).Single();
                    JClue eight = all.Where(x => x.Value == 800 || x.Value == 1600).Take(1).Select(x => new JClue(x.Answer, x.Category, x.Clue, 800)).Single();
                    JClue ten = all.Where(x => x.Value == 1000 || x.Value == 2000).Take(1).Select(x => new JClue(x.Answer, x.Category, x.Clue, 1000)).Single();

                    result.Add(category, new List<JClue> {two, four, six, eight, ten});
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
            JeopardyClue clue = await JeopardyCollection.AsQueryable().Where(x => x.Value > 2000 || x.Value == null).Sample(1).SingleAsync();

            return new JClue(clue.Answer, clue.Category, clue.Clue, 0);
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
            DateTime now = DateTime.UtcNow.AddMinutes(-15);
            return await EventCollection.Find(x => x.StartDate > now && !x.Deleted).ToListAsync();
        }

        public async Task<List<Event>> GetActiveEventsByHostAsync(ulong hostId)
        {
            DateTime now = DateTime.UtcNow;
            return await EventCollection.Find(x => x.Host == hostId && x.StartDate > now && !x.Deleted).ToListAsync();
        }

        public async Task<List<Event>> GetActiveEventsByGuild(ulong guildId)
        {
            DateTime now = DateTime.UtcNow;
            return await EventCollection.Find(x => x.GuildId == guildId && x.StartDate > now && !x.Deleted).ToListAsync();
        }

        public bool GetActiveEventAsync(ulong messageId, out Event e)
        {
            DateTime now = DateTime.UtcNow;
            e = EventCollection.Find(x => x.MessageId == messageId && x.StartDate > now && !x.Deleted).Limit(1).SingleOrDefault();
            return e != null;
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