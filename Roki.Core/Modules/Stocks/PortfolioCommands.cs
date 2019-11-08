using Discord.Commands;
using Roki.Modules.Stocks.Services;

namespace Roki.Modules.Stocks
{
    public partial class Stocks
    {
        [Group]
        public class PortfolioCommands : RokiSubmodule<PortfolioService>
        {
            
        }
    }
}