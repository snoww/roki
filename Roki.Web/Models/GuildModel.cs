using System;
using System.Collections.Generic;

namespace Roki.Web.Models
{
    public class Guild
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public ulong OwnerId { get; set; }
        public int ChannelCount { get; set; }
        public int MemberCount { get; set; }
        public int EmoteCount { get; set; }
        public string RegionId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool Available { get; set; } = true;
        public Dictionary<string, XpReward> XpRewards { get; set; } = new();
        public Dictionary<string, Listing> Store { get; set; } = new();
        public GuildConfig Config { get; set; } = new();
    }
    
    public class XpReward
    {
        public int Level { get; set; }
        public string Type { get; set; }
        public string Reward { get; set; }
    }

    public class Listing
    {
        public ulong SellerId { get; set; }
        public string Name { get; set; }
        public string Details { get; set; }
        public string Description { get; set; } = "-";
        public string Category { get; set; }
        public string Type { get; set; } = "OneTime";
        public int? SubscriptionDays { get; set; }
        public long Cost { get; set; }
        public int Quantity { get; set; } = 1;
    }
    
    public class GuildConfig
    {
        public string Prefix { get; set; } = ".";

        #region Default

        public bool Logging { get; set; }
        public bool CurrencyGeneration { get; set; } = true;
        public bool XpGain { get; set; } = true;
        public string MuteRole { get; set; }
        public Dictionary<string, bool> Modules { get; set; } = new();
        public Dictionary<string, bool> Commands { get; set; } = new();

        #endregion
        
        #region Currency

        public double CurrencyGenerationChance { get; set; } = 0.02;
        public int CurrencyGenerationCooldown { get; set; } = 10;
        public string CurrencyIcon { get; set; } = "<:stone:269130892100763649>";
        public string CurrencyName { get; set; } = "Stone";
        public string CurrencyNamePlural { get; set; } = "Stones";
        public int CurrencyDropAmount { get; set; } = 1;
        public int CurrencyDropAmountMax { get; set; } = 5;
        public int CurrencyDropAmountRare { get; set; } = 100;

        #endregion

        #region Xp

        public int XpPerMessage { get; set; } = 5;
        public int XpCooldown { get; set; } = 5;
        public double XpFastCooldown { get; set; } = 2.5; 

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
        public int TriviaEasy { get; set; } = 100;
        public int TriviaMedium { get; set; } = 300;
        public int TriviaHard { get; set; } = 500;

        #endregion
    }
}