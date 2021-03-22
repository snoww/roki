using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roki.Web.Models
{
    public class DiscordGuild
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
    }

    public class Guild
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public ulong OwnerId { get; set; }
        public List<ulong> Moderators { get; } = new();
        public bool Available { get; set; } = true;

        public virtual GuildConfig GuildConfig { get; set; }
        // public virtual ICollection<Event> Events { get; set; } = new List<Event>();
        // public virtual ICollection<StoreItem> Items { get; set; } = new List<StoreItem>();
        // public virtual ICollection<Quote> Quotes { get; set; } = new List<Quote>();
        // public virtual ICollection<XpReward> XpRewards { get; set; } = new List<XpReward>();

        public Guild(ulong id, string name, string icon, ulong ownerId)
        {
            Id = id;
            Name = name;
            Icon = icon;
            OwnerId = ownerId;
        }
    }

    public class GuildConfig
    {
        public ulong GuildId { get; set; }

        #region Core

        public string Prefix { get; set; } = ".";
        public bool Logging { get; set; } = false;
        public bool CurrencyGen { get; set; } = true;
        public bool XpGain { get; set; } = true;
        public bool ShowHelpOnError { get; set; } = true;

        #endregion

        #region Currency

        public string CurrencyIcon { get; set; } = "<:stone:269130892100763649>";
        public string CurrencyName { get; set; } = "Stone";
        public string CurrencyNamePlural { get; set; } = "Stones";
        public double CurrencyGenerationChance { get; set; } = 0.02;
        public int CurrencyGenerationCooldown { get; set; } = 60;
        public int CurrencyDropAmount { get; set; } = 1;
        public int CurrencyDropAmountMax { get; set; } = 5;
        public int CurrencyDropAmountRare { get; set; } = 100;
        public long CurrencyDefault { get; set; } = 1000;
        public decimal InvestingDefault { get; set; } = 5000;

        #endregion

        #region Xp

        public int XpPerMessage { get; set; } = 5;
        public int XpCooldown { get; set; } = 300;
        public int XpFastCooldown { get; set; } = 150;
        public int NotificationLocation { get; set; } = 1;

        #endregion

        #region BetFlip

        public int BetFlipMin { get; set; } = 2;
        public double BetFlipMultiplier { get; set; } = 1.95;

        #endregion

        #region BetFlipMulti

        public double BetFlipMMinMultiplier { get; set; } = 2;
        public int BetFlipMMinGuesses { get; set; } = 5;
        public double BetFlipMMinCorrect { get; set; } = 0.75;
        public double BetFlipMMultiplier { get; set; } = 1.1;

        #endregion

        #region BetDice

        public int BetDiceMin { get; set; } = 10;

        #endregion

        #region BetRoll

        public int BetRollMin { get; set; } = 1;
        public double BetRoll71Multiplier { get; set; } = 2.5;
        public double BetRoll92Multiplier { get; set; } = 4;
        public double BetRoll100Multiplier { get; set; } = 10;

        #endregion

        #region Trivia

        public double TriviaMinCorrect { get; set; } = 0.6;
        public int TriviaEasy { get; set; } = 100;
        public int TriviaMedium { get; set; } = 300;
        public int TriviaHard { get; set; } = 500;

        #endregion

        #region Jeopardy

        public double JeopardyWinMultiplier { get; set; } = 1;

        #endregion

        [JsonIgnore] 
        public virtual Guild Guild { get; set; }
    }
    
        public class GuildConfigUpdate
    {
        public string Prefix { get; set; }
        [JsonPropertyName("guild_logging")]
        public string Logging { get; set; }
        [JsonPropertyName("guild_curr")]
        public string CurrencyGen { get; set; }
        [JsonPropertyName("guild_xp")]
        public string XpGain { get; set; }
        [JsonPropertyName("guild_show_help")]
        public string ShowHelpOnError { get; set; }
        [JsonPropertyName("guild_curr_icon")]
        public string CurrencyIcon { get; set; }
        [JsonPropertyName("guild_curr_name")]
        public string CurrencyName { get; set; }
        [JsonPropertyName("guild_curr_name_p")]
        public string CurrencyNamePlural { get; set; }
        [JsonPropertyName("guild_curr_prob")]
        public string CurrencyGenerationChance { get; set; }
        [JsonPropertyName("guild_curr_cd")]
        public string CurrencyGenerationCooldown { get; set; }
        [JsonPropertyName("guild_curr_drop")]
        public string CurrencyDropAmount { get; set; }
        [JsonPropertyName("guild_curr_drop_max")]
        public string CurrencyDropAmountMax { get; set; }
        [JsonPropertyName("guild_curr_drop_rare")]
        public string CurrencyDropAmountRare { get; set; }
        [JsonPropertyName("guild_curr_default")]
        public string CurrencyDefault { get; set; }
        [JsonPropertyName("guild_inv_default")]
        public string InvestingDefault { get; set; }
        [JsonPropertyName("guild_xp_pm")]
        public string XpPerMessage { get; set; }
        [JsonPropertyName("guild_xp_cd")]
        public string XpCooldown { get; set; }
        [JsonPropertyName("guild_xp_cd_fast")]
        public string XpFastCooldown { get; set; }
        [JsonPropertyName("guild_xp_notif")]
        public string NotificationLocation { get; set; }
        [JsonPropertyName("guild_bf_min")]
        public string BetFlipMin { get; set; }
        [JsonPropertyName("guild_bf_mult")]
        public string BetFlipMultiplier { get; set; }
        [JsonPropertyName("guild_bfm_min")]
        public string BetFlipMMinMultiplier { get; set; }
        [JsonPropertyName("guild_bfm_min_guess")]
        public string BetFlipMMinGuesses { get; set; }
        [JsonPropertyName("guild_bfm_min_correct")]
        public string BetFlipMMinCorrect { get; set; }
        [JsonPropertyName("guild_bfm_mult")]
        public string BetFlipMMultiplier { get; set; }
        [JsonPropertyName("guild_bd_min")]
        public string BetDiceMin { get; set; }
        [JsonPropertyName("guild_br_min")]
        public string BetRollMin { get; set; }
        [JsonPropertyName("guild_br_71")]
        public string BetRoll71Multiplier { get; set; }
        [JsonPropertyName("guild_br_92")]
        public string BetRoll92Multiplier { get; set; }
        [JsonPropertyName("guild_br_100")]
        public string BetRoll100Multiplier { get; set; }
        [JsonPropertyName("guild_trivia_min")]
        public string TriviaMinCorrect { get; set; }
        [JsonPropertyName("guild_trivia_easy")]
        public string TriviaEasy { get; set; }
        [JsonPropertyName("guild_trivia_med")]
        public string TriviaMedium { get; set; }
        [JsonPropertyName("guild_trivia_hard")]
        public string TriviaHard { get; set; }
        [JsonPropertyName("guild_jeopardy_mult")]
        public string JeopardyWinMultiplier { get; set; }
    }

}