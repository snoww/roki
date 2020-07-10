using System.Threading.Tasks;
using Discord.Commands;
using Roki.Common;
using Roki.Common.Attributes;
using Roki.Modules.Games.Common;

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class ChessCommands : RokiSubmodule
        {
            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            [RokiOptions(typeof(ChessArgs))]
            public async Task ChessChallenge(params string[] args)
            {
                var opts = OptionsParser.ParseFrom(new ChessArgs(), args);
                
            }
        }
    }
}