namespace Roki.Core.Services
{
    public class Properties
    {
        public string Prefix { get; set; } = ".";
        public ulong BotId { get; set; } = 549644503351296040;

        #region Currency

        public double CurrencyGenerationChance { get; set; } = 0.02;
        public int CurrencyGenerationCooldown { get; set; } = 10;
        public ulong[] CurrencyGenIgnoredChannels { get; set; }
        public string CurrencyIcon { get; set; } = "<:stone:269130892100763649>";
        public string CurrencyName { get; set; } = "Stone";
        public string CurrencyNamePlural { get; set; } = "Stones";
        public int CurrencyDropAmount { get; set; } = 1;
        public int? CurrencyDropAmountMax { get; set; } = 5;
        public int? CurrencyDropAmountRare { get; set; } = 100;

        #endregion

        #region Lottery

        public double Lottery4 { get; set; } = 0.055;
        public double Lottery5 { get; set; } = 0.045;
        public double LotteryJackpot { get; set; } = 0.9;
        public int LotteryDraw { get; set; } = 7;
        public int LotteryTicketCost { get; set; } = 5;
        public long LotteryMin {get; set;} = 1000;

        #endregion

        #region Xp

        public int XpPerMessage { get; set; } = 5;
        public int XpCooldown { get; set; } = 5;

        #endregion

        #region BetFlip

        public int BetFlipMin { get; set; } = 2;
        public double BetFlipMultiplier { get; set; } = 1.95;
        public int BetFlipMMinGuesses { get; set; } = 5;
        public double BetFlipMMinCorrect { get; set; } = 0.75;
        public double BetFlipMMultiplier { get; set; } = 1.1;

        #endregion

        #region BetDie

        public int BetDieMin { get; set; } = 10;

        #endregion

        #region BetRoll

        public int BetRollMin { get; set; } = 1;
        public double BetRoll71Multiplier { get; set; } = 2.5;
        public int BetRoll92Multiplier { get; set; } = 4;
        public int BetRoll100Multiplier { get; set; } = 10;

        #endregion

        #region Trivia

        public double TriviaMinCorrect { get; set; } = 0.6;
        public int TriviaEasy { get; set; } = 1;
        public int TriviaMedium { get; set; } = 3;
        public int TriviaHard { get; set; } = 5;

        #endregion
    }
}