using System.Threading.Tasks;
using Roki.Core.Services;

namespace Roki.Modules.Gambling
{
    public partial class Gambling : RokiTopLevelModule
    {
        private readonly DbService _db;

        public Gambling(DbService db)
        {
            _db = db;
        }
    }
}