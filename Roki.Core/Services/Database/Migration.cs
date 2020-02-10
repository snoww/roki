using System;
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
                var collection = _mongo.GetCollection<Quote>("quotes");

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
                var collection = _mongo.GetCollection<Listing>("store");

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
                    Logger.Info("Adding transaction {number}", transaction.Id);
                    var mongoTransaction = new Transaction
                    {
                        Amount = transaction.Amount,
                        ChannelId = transaction.ChannelId,
                        From = transaction.From,
                        GuildId = transaction.GuildId,
                        MessageId = transaction.MessageId,
                        Reason = transaction.Reason,
                        To = transaction.To,
                        Date = transaction.TransactionDate
                    };
                    
                    await collection.InsertOneAsync(mongoTransaction).ConfigureAwait(false);
                }
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
                    Logger.Info("Adding messageid [{number}]", message.MessageId);
                    var findMessage = await collection.Find(m => m.MessageId == message.MessageId).FirstOrDefaultAsync();
                    if (findMessage != null)
                    {
                        var update = Builders<Message>.Update.Push(x => x.Edits, new Edit
                        {
                            Content = message.Content,
                            EditedTimestamp = message.EditedTimestamp ?? message.Timestamp
                        });
                        
                        await collection.UpdateOneAsync(m => m.MessageId == message.MessageId, update).ConfigureAwait(false);
                        continue;
                    }
                    
                    var mongoMessage = new Message
                    {
                        AuthorId = message.AuthorId,
                        ChannelId = message.ChannelId,
                        Content = message.Content,
                        GuildId = message.GuildId,
                        IsDeleted = message.IsDeleted,
                        MessageId = message.MessageId,
                        Timestamp = message.Timestamp
                    };

                    if (message.EditedTimestamp != null)
                    {
                        mongoMessage.Edits.Add(new Edit{Content = message.Content, EditedTimestamp = message.EditedTimestamp ?? message.Timestamp});
                    }

                    await collection.InsertOneAsync(mongoMessage).ConfigureAwait(false);
                }
                
                sw.Stop();
                Logger.Info("Took {time:l}", sw.Elapsed.ToReadableString());
            });

            return Task.CompletedTask;
        }

        public Task MigrateJeopardy()
        {
            var _ = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                Logger.Info("Starting jeopardy migration");

                var collection = _mongo.GetCollection<JClue>("jeopardy");
                using var uow = _db.GetDbContext();
                var query = from clue in uow.Context.Set<Clues>()
                            join document in uow.Context.Set<Documents>() on clue.Id equals document.Id
                            join classification in uow.Context.Set<Classification>() on clue.Id equals classification.ClueId
                            join categories in uow.Context.Set<Categories>() on classification.CategoryId equals categories.Id
                            select new JClue
                            {
                                Category = categories.Category,
                                Clue = document.Clue,
                                Answer = document.Answer,
                                Value = clue.Value
                            };

                var count = 0;
                foreach (var clue in query)
                {
                    Logger.Info("Adding clue {counter}", count++);
                    await collection.InsertOneAsync(clue).ConfigureAwait(false);
                }
                
                sw.Stop();
                Logger.Info("Took {time:l}", sw.Elapsed.ToReadableString());
            });
            
            return Task.CompletedTask;
        }
    }
}