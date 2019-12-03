using Roki.Core.Services;

namespace Roki.Modules.Games.Common
{
    public class Jeopardy
    {
        private readonly DbService _db;
        public Jeopardy(DbService db)
        {
            _db = db;
        }
    }
}