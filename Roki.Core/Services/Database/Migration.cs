using MongoDB.Driver;

namespace Roki.Services.Database
{
    public class Migration
    {
        private readonly DbService _db;
        private readonly IMongoDatabase _mongo = MongoService.Instance.Database;

        public Migration(DbService db)
        {
            _db = db;
        }
    }
}