using System.Threading.Tasks;
using Roki.Common.Attributes;
using Roki.Modules.Stocks.Services;

namespace Roki.Modules.Stocks
{
    public class Stocks : RokiTopLevelModule<StocksService>
    {
        [RokiCommand, Usage, Description, Aliases]
        public async Task StocksStats(string symbol)
        {
            
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task Company(string symbol)
        {
            
        }
        
        [RokiCommand, Usage, Description, Aliases]
        public async Task News(string symbol)
        {
            
        }
    }
}