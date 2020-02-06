using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using NLog;
using Roki.Extensions;
using Roki.Services.Database.Maps;

namespace Roki.Services.Database
{
    public class Migration
    {
        private readonly DbService _db;
        private static readonly IMongoDatabase Mongo = MongoService.Instance.Database;
        private static Logger Logger = LogManager.GetCurrentClassLogger();

        public Migration(DbService db)
        {
            _db = db;
        }

        public Task MigrateUsers()
        {
            var _ = Task.Run(async () =>
            {
                Logger.Info("Starting users migration");
                using var uow = _db.GetDbContext();
                var users = uow.Context.Users.OrderBy(x => x.Id);
                var collection = Mongo.GetCollection<User>("users");

                var count = 0;
                foreach (var user in users)
                {
                    Logger.Info("{counter}: Adding {user:l}#{discrim}", count++, user.Username, user.Discriminator);
                    var mongoUser = new User
                    {
                        Username = user.Username,
                        AvatarId = user.AvatarId,
                        Currency = user.Currency,
                        Discriminator = int.Parse(user.Discriminator),
                        Id = user.UserId,
                        Xp = user.TotalXp,
                        Inventory = user.Inventory?.Deserialize<List<Item>>(),
                        InvestingAccount = user.InvestingAccount,
                        LastLevelUp = user.LastLevelUp,
                        LastXpGain = user.LastLevelUp,
                        Notification = user.NotificationLocation,
                        Portfolio = user.Portfolio?.Deserialize<List<Investment>>()
                    };
                    
                    await collection.InsertOneAsync(mongoUser).ConfigureAwait(false);
                }
            });

            return Task.CompletedTask;
        }

        public Task MigrateQuotes()
        {
            var _ = Task.Run(async () =>
            {
                Logger.Info("Starting quotes migration");
                using var uow = _db.GetDbContext();
                var quotes = uow.Context.Quotes.OrderBy(x => x.Id);
                var collection = Mongo.GetCollection<Quote>("quotes");

                foreach (var quote in quotes)
                {
                    Logger.Info("Adding quote {number}", quote.Id);
                    var mongoQuote = new Quote
                    {
                        AuthorId = quote.AuthorId,
                        Context = quote.Context,
                        DateAdded = quote.DateAdded,
                        GuildId = quote.GuildId,
                        Keyword = quote.Keyword,
                        Text = quote.Text,
                        UseCount = quote.UseCount
                    };
                    
                    await collection.InsertOneAsync(mongoQuote).ConfigureAwait(false);
                }
            });

            return Task.CompletedTask;
        }
        
        public Task MigrateStore()
        {
            var _ = Task.Run(async () =>
            {
                Logger.Info("Starting store migration");
                using var uow = _db.GetDbContext();
                var listings = uow.Context.Listings.OrderBy(x => x.Id);
                var collection = Mongo.GetCollection<Listing>("store");

                foreach (var listing in listings)
                {
                    Logger.Info("Adding store item {number}", listing.Id);
                    var mongoListing = new Listing
                    {
                        Category = listing.Category,
                        Cost = listing.Cost,
                        Description = listing.Description,
                        Details = listing.Details,
                        Item = listing.Item,
                        ListDate = listing.ListDate,
                        Quantity = listing.Quantity,
                        SellerId = listing.SellerId,
                        SubscriptionDays = listing.SubscriptionDays,
                        Type = listing.Type
                    };
                    
                    await collection.InsertOneAsync(mongoListing).ConfigureAwait(false);
                }
            });

            return Task.CompletedTask;
        }
    }
}