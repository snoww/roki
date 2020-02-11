using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MongoDB.Driver;
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

        
        Task<User> GetOrAddUserAsync(IUser user);
        Task<User> UpdateUserAsync(IUser after);
        Task<int> UpdateUserXp(User user, bool doubleXp);
        Task<bool> UpdateUserCurrency(User user, long amount);
        
        Task<Channel> GetOrAddChannelAsync(ITextChannel channel);
        Task DeleteChannelAsync(ITextChannel channel);
        Task UpdateChannelAsync(ITextChannel after);
        bool IsLoggingEnabled(ITextChannel channel);
        
        Task<Guild> GetOrAddGuildAsync(SocketGuild guild);
        Task ChangeGuildAvailabilityAsync(SocketGuild guild, bool available);
        Task UpdateGuildAsync(SocketGuild after);
        
        Task AddMessageAsync(SocketMessage message);
        Task AddMessageEditAsync(SocketMessage after);
        Task MessageDeletedAsync(ulong messageId);

        Task AddTransaction(Transaction transaction);

    }

    public class MongoContext : IMongoContext
    {
        private readonly IMongoDatabase _database;
        
        public IMongoCollection<Message> MessageCollection { get; }
        public IMongoCollection<Channel> ChannelCollection { get; }
        public IMongoCollection<Guild> GuildCollection { get; }
        public IMongoCollection<User> UserCollection { get; }
        public IMongoCollection<Transaction> TransactionCollection { get; }

        public MongoContext(IMongoDatabase database)
        {
            _database = database;
            
            MessageCollection = database.GetCollection<Message>("messages");
            ChannelCollection = database.GetCollection<Channel>("channels");
            GuildCollection = database.GetCollection<Guild>("guilds");
            UserCollection = database.GetCollection<User>("users");
            TransactionCollection = database.GetCollection<Transaction>("transactions");
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

        public async Task<User> UpdateUserAsync(IUser after)
        {
            var updateUser = Builders<User>.Update.Set(u => u.Id, after.Id)
                .Set(u => u.Username, after.Username)
                .Set(u => u.Discriminator, int.Parse(after.Discriminator))
                .Set(u => u.AvatarId, after.AvatarId);

            return await UserCollection.FindOneAndUpdateAsync<User>(u => u.Id == after.Id, updateUser,
                new FindOneAndUpdateOptions<User>{ReturnDocument = ReturnDocument.After, IsUpsert = true}).ConfigureAwait(false);
        }

        public async Task<int> UpdateUserXp(User user, bool doubleXp)
        {
            var updateXp = Builders<User>.Update.Inc(u => u.Xp,
                doubleXp ? Roki.Properties.XpPerMessage * 2 : Roki.Properties.XpPerMessage);
            
            return await UserCollection.FindOneAndUpdateAsync(u => u.Id == user.Id, updateXp,
                    new FindOneAndUpdateOptions<User, int> {ReturnDocument = ReturnDocument.After})
                .ConfigureAwait(false);
        }

        public async Task<bool> UpdateUserCurrency(User user, long amount)
        {
            if (user.Currency + amount < 0)
                return false;
            var updateCurrency = Builders<User>.Update.Inc(u => u.Currency, amount);
            await UserCollection.UpdateOneAsync(u => u.Id == user.Id, updateCurrency).ConfigureAwait(false);
            return true;
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
                Timestamp = message.Timestamp
            }).ConfigureAwait(false);
        }

        public async Task AddMessageEditAsync(SocketMessage after)
        {
            var update = Builders<Message>.Update.Push(m => m.Edits, new Edit
            {
                Content = after.Content,
                Attachments = after.Attachments.Select(m => m.Url).ToList(),
                EditedTimestamp = after.EditedTimestamp ?? after.Timestamp
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
    }
}