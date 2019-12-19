using CommandLine;
using Roki.Common;

namespace Roki.Modules.Games.Common
{
    public class JeopardyOptions : ICommandOptions
    {
        [Option('c', "categories", Required = false, Default = false, HelpText = "Choose the number of categories in the Jeopardy! game. Default is 2, max is 6.")]
        public int NumCategories { get; set; } = 2;
        [Option('f', "final", Required = false, Default = false, HelpText = "Whether to have a Final Jeopardy! at the end of the game. Default is false.")]
        public bool FinalJeopardy { get; set; } = false;
        public void NormalizeOptions()
        {
            if (NumCategories > 5) 
                NumCategories = 5;
            if (NumCategories <= 0)
                NumCategories = 2;
        }
    }
}