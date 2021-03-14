using CommandLine;
using Roki.Common;

namespace Roki.Modules.Currency.Common
{
    public class TransferOptions : ICommandOptions
    {
        [Value(0, Required = true, MetaValue = "AMOUNT", HelpText = "The amount to transfer")]
        public long Amount { get; set; }
        
        [Value(1, Required = true, MetaValue = "TO/FROM", HelpText = "Direction of transfer")]
        public Direction Direction { get; set; }

        [Value(2, Required = true, MetaValue = "CASH/INVESTING", HelpText = "The target account")]
        public Account Account { get; set; }

        public void NormalizeOptions()
        {
        }
    }
}