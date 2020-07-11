using CommandLine;
using Discord;
using Roki.Common;

namespace Roki.Modules.Games.Common
{
    public class ChessArgs : ICommandArgs
    {
        [Option('t', "time", Required = false, Default = 600, HelpText = "Maximum time for chess game in seconds. Default is 600 (10 min).")]
        public int Time { get; set; }
        
        [Option('i', "inc", Required = false, Default = 0, HelpText = "Increment for chess game in seconds. Default is 0.")]
        public int Increment { get; set; }
        
        [Option('c', "color", Required = false, Default = ChessColor.Random, HelpText = "Choose what color you'd wish to play as, choose from white, black, and random. Default is random.")]
        public ChessColor Color { get; set; }
        
        public IGuildUser ChallengeTo { get; set; }

        public void NormalizeArgs()
        {
            if (Time <= 0 || Time > 10800)
            {
                Time = 600;
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