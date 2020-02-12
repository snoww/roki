using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using NLog;
using Roki.Extensions;
using Roki.Services.Database.Data;
using Roki.Services.Database.Maps;

namespace Roki.Services.Database
{
    public class Migration
    {
        private readonly DbService _db;
        private readonly IMongoDatabase _mongo;
        private static Logger Logger = LogManager.GetCurrentClassLogger();

        public Migration(DbService db, IMongoDatabase mongo)
        {
            _db = db;
            _mongo = mongo;
        }

        public Task MigrateUsers()
        {
            var _ = Task.Run(async () =>
            {
                Logger.Info("Starting users migration");
                using var uow = _db.GetDbContext();
                var users = uow.Context.Users.OrderBy(x => x.Id);
                var collection = _mongo.GetCollection<User>("users");

                foreach (var user in users)
                {
                    var mongoUser = new User
                    {
                        Username = user.Username,
                        AvatarId = user.AvatarId,
                        Currency = user.Currency,
                        Discriminator = int.Parse(user.Discriminator),
                        Id = user.UserId,
                        Xp = user.TotalXp,
                        Inventory = user.Inventory?.Deserialize<List<Item>>() ?? new List<Item>(),
                        InvestingAccount = user.InvestingAccount,
                        LastLevelUp = user.LastLevelUp.DateTime,
                        LastXpGain = user.LastLevelUp.DateTime,
                        Notification = user.NotificationLocation,
                        Portfolio = user.Portfolio?.Deserialize<List<Investment>>() ?? new List<Investment>()
                    };
                    
                    await collection.InsertOneAsync(mongoUser).ConfigureAwait(false);
                }

                Logger.Info("Finished users import");
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
                var collection = _mongo.GetCollection<Quote>("quotes");

                foreach (var quote in quotes)
                {
                    var mongoQuote = new Quote
                    {
                        Id = quote.DateAdded.HasValue ? ObjectId.GenerateNewId(quote.DateAdded.Value.UtcDateTime) : ObjectId.GenerateNewId(),
                        AuthorId = quote.AuthorId,
                        Context = quote.Context,
                        GuildId = quote.GuildId,
                        Keyword = quote.Keyword,
                        Text = quote.Text,
                        UseCount = quote.UseCount
                    };
                    
                    await collection.InsertOneAsync(mongoQuote).ConfigureAwait(false);
                }
                
                Logger.Info("Finished quotes import");
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
                var collection = _mongo.GetCollection<Listing>("store");

                foreach (var listing in listings)
                {
                    var mongoListing = new Listing
                    {
                        Id = ObjectId.GenerateNewId(listing.ListDate.UtcDateTime),
                        Category = listing.Category,
                        Cost = listing.Cost,
                        Description = listing.Description,
                        Details = listing.Details,
                        Name = listing.Item,
                        Quantity = listing.Quantity,
                        SellerId = listing.SellerId,
                        SubscriptionDays = listing.SubscriptionDays,
                        Type = listing.Type
                    };
                    
                    await collection.InsertOneAsync(mongoListing).ConfigureAwait(false);
                }
                
                Logger.Info("Finished store import");
            });

            return Task.CompletedTask;
        }
        
        public Task MigrateTransactions()
        {
            var _ = Task.Run(async () =>
            {
                Logger.Info("Starting transaction history migration");
                using var uow = _db.GetDbContext();
                var transactions = uow.Context.Transactions.OrderBy(x => x.Id);
                var collection = _mongo.GetCollection<Transaction>("transactions");

                foreach (var transaction in transactions)
                {
                    var mongoTransaction = new Transaction
                    {
                        Id = ObjectId.GenerateNewId(transaction.TransactionDate.UtcDateTime),
                        Amount = transaction.Amount,
                        ChannelId = transaction.ChannelId,
                        From = transaction.From,
                        GuildId = transaction.GuildId,
                        MessageId = transaction.MessageId,
                        Reason = transaction.Reason,
                        To = transaction.To
                    };
                    
                    await collection.InsertOneAsync(mongoTransaction).ConfigureAwait(false);
                }
                
                Logger.Info("Finished transaction import");
            });

            return Task.CompletedTask;
        }
        
        public Task MigrateMessages()
        {
            var _ = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                Logger.Info("Starting messages migration");
                using var uow = _db.GetDbContext();
                var messages = uow.Context.Messages.OrderBy(x => x.Timestamp);
                var collection = _mongo.GetCollection<Message>("messages");

                foreach (var message in messages)
                {
                    var findMessage = await collection.Find(m => m.Id == message.MessageId).FirstOrDefaultAsync();
                    if (findMessage != null)
                    {
                        var update = Builders<Message>.Update.Push(x => x.Edits, new Edit
                        {
                            Content = message.Content,
                            EditedTimestamp = message.EditedTimestamp?.UtcDateTime ?? message.Timestamp.UtcDateTime
                        });
                        
                        await collection.UpdateOneAsync(m => m.Id == message.MessageId, update).ConfigureAwait(false);
                        continue;
                    }
                    
                    var mongoMessage = new Message
                    {
                        AuthorId = message.AuthorId,
                        ChannelId = message.ChannelId,
                        Content = message.Content,
                        GuildId = message.GuildId,
                        IsDeleted = message.IsDeleted,
                        Id = message.MessageId,
                        Timestamp = message.Timestamp.UtcDateTime
                    };

                    if (message.EditedTimestamp != null)
                    {
                        mongoMessage.Edits.Add(new Edit{Content = message.Content, EditedTimestamp = message.EditedTimestamp.Value.UtcDateTime});
                    }

                    await collection.InsertOneAsync(mongoMessage).ConfigureAwait(false);
                }
                
                sw.Stop();
                Logger.Info("Finished message import in {time:l}", sw.Elapsed.ToReadableString());
            });

            return Task.CompletedTask;
        }

        public Task MigrateJeopardy()
        {
            var _ = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                Logger.Info("Starting jeopardy migration");

                var collection = _mongo.GetCollection<JeopardyClue>("jeopardy");
                using var uow = _db.GetDbContext();
                var query = from clue in uow.Context.Set<Clues>()
                            join document in uow.Context.Set<Documents>() on clue.Id equals document.Id
                            join classification in uow.Context.Set<Classification>() on clue.Id equals classification.ClueId
                            join categories in uow.Context.Set<Categories>() on classification.CategoryId equals categories.Id
                            select new JeopardyClue
                            {
                                Category = categories.Category,
                                Clue = document.Clue,
                                Answer = document.Answer,
                                Value = clue.Value
                            };

                foreach (var clue in query)
                {
                    await collection.InsertOneAsync(clue).ConfigureAwait(false);
                }
                
                sw.Stop();
                Logger.Info("Took {time:l}", sw.Elapsed.ToReadableString());
            });
            
            return Task.CompletedTask;
        }
    }
}