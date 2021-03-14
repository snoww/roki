using CommandLine;
using Discord;
using Roki.Common;

namespace Roki.Modules.Games.Common
{
    public class ChessOptions : ICommandOptions
    {
        [Option('t', "time", MetaValue = "MINUTES", Required = false, Default = 10, HelpText = "Maximum time for chess game in minutes. Default is 10 min.")]
        public int Time { get; set; }
        
        [Option('i', "inc", MetaValue = "SECONDS", Required = false, Default = 0, HelpText = "Increment for chess game in seconds. Default is 0.")]
        public int Increment { get; set; }
        
        [Option('c', "color", MetaValue = "COLOR", Required = false, Default = ChessColor.Random, HelpText = "Choose what color you'd wish to play as. White/Black/Random. Default is random.")]
        public ChessColor Color { get; set; }
        
        public IGuildUser ChallengeTo { get; set; }

        public void NormalizeOptions()
        {
            if (Time <= 0 || Time > 180)
            {
                Time = 10;
            }

            if (Increment < 0 || Increment > 60)
            {
                Increment = 0;
            }
        }
    }

    public enum ChessColor
    {
        White,
        Black,
        Random
    }
}