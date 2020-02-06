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
    }
}