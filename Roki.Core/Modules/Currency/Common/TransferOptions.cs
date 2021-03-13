using CommandLine;
using Roki.Common;

namespace Roki.Modules.Currency.Common
{
    public class TransferOptions : ICommandOptions
    {
        [Value(0, Required = true, MetaValue = "number", HelpText = "The amount to transfer")]
        public long Amount { get; set; }
        
        [Value(1, Required = true, MetaValue = "to/from", HelpText = "Direction of transfer")]
        public Direction Direction { get; set; }

        [Value(2, Required = true, MetaValue = "cash/investing", HelpText = "The target account")]
        public Account Account { get; set; }

        public void NormalizeOptions()
        {
        }
    }
}