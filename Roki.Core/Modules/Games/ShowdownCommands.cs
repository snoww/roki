using System.Threading.Tasks;
using Discord.Commands;
using Roki.Common.Attributes;

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class ShowdownCommands : RokiSubmodule
        {
            [RokiCommand, Description, Aliases, Usage]
            public async Task BetPokemonGame()
            {
                
            }
        }
    }
}