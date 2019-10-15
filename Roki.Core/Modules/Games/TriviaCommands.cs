using System.Threading.Tasks;
using Discord.Commands;
using Roki.Common.Attributes;
using Roki.Modules.Games.Services;

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class TriviaCommands : RokiSubmodule<TriviaService>
        {
            [RokiCommand, Description, Usage, Aliases]
            public async Task Trivia()
            {
                
            }
        }
    }
}