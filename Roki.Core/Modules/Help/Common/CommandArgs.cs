using CommandLine;
using Roki.Common;

namespace Roki.Modules.Help.Common
{
    public class CommandArgs : ICommandArgs
    {
        public enum ViewType
        {
            Hide,
            Cross,
            All
        }
        
        [Option('v', "view", Required = false, Default = ViewType.Hide, HelpText = "Specifies how to output the list of commands. `hide` - Hide commands which you can't use. `cross` - Cross out commands which you can't use. `all` - Show all. Default is `hide`")]
        public ViewType View { get; set; } = ViewType.Hide;

        public void NormalizeArgs()
        {
        }
    }
}