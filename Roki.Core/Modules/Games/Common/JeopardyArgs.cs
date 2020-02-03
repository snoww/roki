using CommandLine;
using Roki.Common;

namespace Roki.Modules.Games.Common
{
    public class JeopardyArgs : ICommandArgs
    {
        [Option('c', "categories", Required = false, Default = false, HelpText = "Choose the number of categories in the Jeopardy! game. Default is 2, max is 6.")]
        public int NumCategories { get; set; } = 2;
        
        public void NormalizeArgs()
        {
            if (NumCategories > 6) 
                NumCategories = 6;
            if (NumCategories <= 0)
                NumCategories = 2;
        }
    }
}